from __future__ import annotations

import json
import subprocess
import time
from datetime import datetime
from pathlib import Path
from typing import Any
from zoneinfo import ZoneInfo

from automation.codex_runner import read_result, run_codex_session
from automation.common import project_root, read_json, run_command, write_json
from automation.git_ops import (
    changed_files,
    check_disallowed_paths,
    commit_all,
    create_batch_worktree,
    create_integration_worktree,
    fast_forward,
    git_output,
    preflight,
)
from automation.progress import parse_progress, update_progress


class SchedulerError(RuntimeError):
    pass


def timestamp() -> str:
    return datetime.now(ZoneInfo("Asia/Shanghai")).isoformat(timespec="seconds")


def state_path(config: dict[str, Any]) -> Path:
    return Path(config["paths"]["runtime"]) / "active-run.json"


def stop_path(config: dict[str, Any]) -> Path:
    return Path(config["paths"]["runtime"]) / "STOP"


def select_next_batch(
    statuses: dict[str, str], batch_config: dict[str, Any]
) -> str | None:
    for batch_id in sorted(batch_config):
        if statuses.get(batch_id) not in {"pending", "in_progress"}:
            continue
        dependencies = batch_config[batch_id].get("depends_on", [])
        if all(statuses.get(dependency) == "completed" for dependency in dependencies):
            return batch_id
    return None


def budget_allows_session(state: dict[str, Any], config: dict[str, Any]) -> bool:
    limit = int(config["limits"]["max_total_tokens"])
    reserve = int(config["limits"]["minimum_remaining_tokens_to_start_session"])
    return limit - int(state.get("total_tokens", 0)) >= reserve


def start_run(config: dict[str, Any], max_batches: int | None = None) -> dict[str, Any]:
    active_path = state_path(config)
    existing = read_json(active_path)
    if existing and existing.get("status") != "completed":
        raise SchedulerError("已有未结束运行；请先查看 status，并使用 resume 继续，避免遗留 worktree")

    flight = preflight(config)
    if not flight.ok:
        raise SchedulerError("真实运行安全检查未通过：\n- " + "\n- ".join(flight.errors))

    root = project_root(config)
    run_id = datetime.now(ZoneInfo("Asia/Shanghai")).strftime("%Y%m%d-%H%M%S")
    integration_path, integration_branch = create_integration_worktree(
        root,
        Path(config["paths"]["worktree_parent"]),
        run_id,
        config["git"]["integration_branch_prefix"],
    )
    limit = max_batches or int(config["limits"]["default_max_batches_per_run"])
    state = {
        "run_id": run_id,
        "status": "running",
        "started_at": timestamp(),
        "updated_at": timestamp(),
        "source_root": str(root),
        "integration_worktree": str(integration_path),
        "integration_branch": integration_branch,
        "max_batches": limit,
        "completed_this_run": 0,
        "total_tokens": 0,
        "current_batch": None,
        "batches": {},
        "sessions": [],
    }
    stop_path(config).unlink(missing_ok=True)
    write_json(active_path, state)
    return run_loop(config, state)


def resume_run(config: dict[str, Any], max_batches: int | None = None) -> dict[str, Any]:
    state = read_json(state_path(config))
    if not state:
        raise SchedulerError("没有可恢复的运行")
    if state.get("status") == "manual_review":
        raise SchedulerError("当前 Batch 等待人工验收；请先 approve，不能自动越过")
    if state.get("status") == "blocked":
        raise SchedulerError("运行已因达到失败或预算上限而 blocked；请先人工检查日志，不能自动重试")
    if state.get("status") == "completed":
        return state
    if max_batches is not None:
        state["max_batches"] = max_batches
    state["completed_this_run"] = 0
    state["status"] = "running"
    state["updated_at"] = timestamp()
    stop_path(config).unlink(missing_ok=True)
    write_json(state_path(config), state)
    return run_loop(config, state)


def run_loop(config: dict[str, Any], state: dict[str, Any]) -> dict[str, Any]:
    integration = Path(state["integration_worktree"])
    while state["completed_this_run"] < state["max_batches"]:
        if stop_path(config).exists():
            return _finish_state(config, state, "stopped", "检测到安全停止标记")
        if not budget_allows_session(state, config):
            return _finish_state(config, state, "blocked", "剩余 Token 预算不足以启动新会话")

        progress = parse_progress(integration / config["project"]["progress_file"])
        statuses = {batch_id: item.status for batch_id, item in progress.items()}
        next_batch = select_next_batch(statuses, config["batches"])
        if next_batch is None:
            if statuses and all(value == "completed" for value in statuses.values()):
                _write_final_report(config, state)
                return _finish_state(config, state, "completed", "全部 Batch 已完成")
            return _finish_state(config, state, "blocked", "没有满足依赖条件的可执行 Batch")

        outcome = execute_batch(config, state, next_batch)
        write_json(state_path(config), state)
        if outcome != "completed":
            return state
        state["completed_this_run"] += 1

    return _finish_state(config, state, "stopped", "达到本次最大 Batch 数量")


def execute_batch(config: dict[str, Any], state: dict[str, Any], batch_id: str) -> str:
    root = Path(state["source_root"])
    existing_batch = state["batches"].get(batch_id)
    if existing_batch and Path(existing_batch["worktree"]).is_dir():
        batch_state = existing_batch
        batch_path = Path(batch_state["worktree"])
        batch_branch = batch_state["branch"]
        batch_state["status"] = "in_progress"
    else:
        batch_path, batch_branch = create_batch_worktree(
            root,
            Path(config["paths"]["worktree_parent"]),
            state["run_id"],
            batch_id,
            config["git"]["batch_branch_prefix"],
            state["integration_branch"],
        )
        batch_state = {
            "status": "in_progress",
            "attempts": 0,
            "tokens": 0,
            "worktree": str(batch_path),
            "branch": batch_branch,
            "base_commit": git_output(batch_path, "rev-parse", "HEAD"),
            "started_at": timestamp(),
            "sessions": [],
        }
        state["batches"][batch_id] = batch_state
    state["current_batch"] = batch_id
    state["updated_at"] = timestamp()
    write_json(state_path(config), state)

    task_path = batch_path / config["project"]["tasks_directory"] / f"batch-{batch_id}.md"
    task_text = task_path.read_text(encoding="utf-8")
    max_retries = int(config["limits"]["max_fix_retries"])
    blocking_feedback = ""

    start_attempt = int(batch_state.get("attempts", 0))
    for attempt in range(start_attempt, max_retries + 1):
        if not budget_allows_session(state, config):
            return _block_batch(config, state, batch_id, "Token 预算不足")
        batch_state["attempts"] = attempt + 1
        model_config = _executor_model(config, batch_id, attempt)
        prompt = _executor_prompt(batch_path, task_text, attempt, blocking_feedback)
        session = run_codex_session(
            config=config,
            cwd=batch_path,
            prompt=prompt,
            model=model_config["model"],
            reasoning_effort=model_config["reasoning_effort"],
            sandbox=config["codex"]["executor_sandbox"],
            schema_path=batch_path / "automation/schemas/executor_result.schema.json",
            log_directory=_batch_log_dir(config, state, batch_id),
            session_name=f"executor-attempt-{attempt + 1}",
            timeout_seconds=int(config["limits"]["executor_timeout_seconds"]),
            stop_file=stop_path(config),
        )
        _record_session(config, state, batch_state, "executor", session.to_dict())
        if batch_state.get("budget_exceeded"):
            return _block_batch(config, state, batch_id, "单 Batch Token 预算已达到上限")
        if session.stopped:
            return _set_batch_state(config, state, batch_id, "stopped", "执行会话被安全停止")
        execution_result = read_result(Path(session.result_file))
        if session.exit_code != 0 or execution_result.get("status") != "completed":
            blocking_feedback = _session_failure_text(session.to_dict(), execution_result)
            if attempt < max_retries:
                continue
            return _block_batch(config, state, batch_id, blocking_feedback)

        deterministic = run_deterministic_checks(config, state, batch_id, batch_path)
        if not deterministic["passed"]:
            blocking_feedback = "\n".join(deterministic["issues"])
            if attempt < max_retries:
                continue
            return _block_batch(config, state, batch_id, blocking_feedback)

        if not budget_allows_session(state, config):
            return _block_batch(config, state, batch_id, "执行完成后 Token 预算不足，未启动验收会话")
        review_model = _reviewer_model(config, batch_id, attempt)
        review_prompt = _reviewer_prompt(task_text, execution_result, deterministic)
        review = run_codex_session(
            config=config,
            cwd=batch_path,
            prompt=review_prompt,
            model=review_model["model"],
            reasoning_effort=review_model["reasoning_effort"],
            sandbox=config["codex"]["reviewer_sandbox"],
            schema_path=batch_path / "automation/schemas/reviewer_result.schema.json",
            log_directory=_batch_log_dir(config, state, batch_id),
            session_name=f"reviewer-attempt-{attempt + 1}",
            timeout_seconds=int(config["limits"]["reviewer_timeout_seconds"]),
            stop_file=stop_path(config),
        )
        _record_session(config, state, batch_state, "reviewer", review.to_dict())
        if batch_state.get("budget_exceeded"):
            return _block_batch(config, state, batch_id, "单 Batch Token 预算已达到上限")
        if review.stopped:
            return _set_batch_state(config, state, batch_id, "stopped", "验收会话被安全停止")
        review_result = read_result(Path(review.result_file))
        decision = review_result.get("decision") if review.exit_code == 0 else "fail"
        if decision == "fail":
            blocking_feedback = json.dumps(review_result, ensure_ascii=False)
            if attempt < max_retries:
                continue
            return _block_batch(config, state, batch_id, blocking_feedback)

        requires_manual = bool(config["batches"][batch_id]["manual_review_required"])
        if decision == "manual_required" or requires_manual:
            return _prepare_manual_review(
                config, state, batch_id, execution_result, review_result, deterministic
            )
        return _complete_batch(
            config, state, batch_id, execution_result, review_result, deterministic, "自动验收通过"
        )

    return _block_batch(config, state, batch_id, "超过最大重试次数")


def run_deterministic_checks(
    config: dict[str, Any], state: dict[str, Any], batch_id: str, worktree: Path
) -> dict[str, Any]:
    log_dir = _batch_log_dir(config, state, batch_id)
    log_dir.mkdir(parents=True, exist_ok=True)
    files = changed_files(worktree)
    disallowed = check_disallowed_paths(files, config["validation"]["disallowed_changed_paths"])
    issues = [f"禁止提交的路径发生变化：{path}" for path in disallowed]
    if config["project"]["progress_file"] in files:
        issues.append("执行 Agent 修改了由调度器负责的 PROGRESS.md")
    batch_state = state["batches"][batch_id]
    current_head = git_output(worktree, "rev-parse", "HEAD")
    if current_head != batch_state.get("base_commit"):
        issues.append("执行 Agent 创建了 Git 提交；提交职责属于调度器")
    result: dict[str, Any] = {
        "passed": not issues,
        "changed_files": files,
        "issues": issues,
        "unity": {"status": "not_run"},
    }

    if config["validation"].get("run_unity_compile", True):
        unity = Path(config["project"]["unity_executable"])
        unity_log = log_dir / "unity-compile.log"
        if not unity.is_file():
            result["unity"] = {"status": "manual_required", "reason": f"Unity 不存在：{unity}"}
        else:
            command = [
                str(unity),
                "-batchmode",
                "-nographics",
                "-quit",
                "-projectPath",
                str(worktree),
                "-logFile",
                str(unity_log),
            ]
            started = time.monotonic()
            try:
                completed = subprocess.run(
                    command,
                    cwd=worktree,
                    text=True,
                    capture_output=True,
                    timeout=int(config["limits"]["unity_timeout_seconds"]),
                )
                result["unity"] = {
                    "status": "pass" if completed.returncode == 0 else "fail",
                    "exit_code": completed.returncode,
                    "duration_seconds": round(time.monotonic() - started, 3),
                    "log": str(unity_log),
                    "stderr": completed.stderr[-2000:],
                }
                if completed.returncode != 0:
                    result["issues"].append("Unity batchmode 编译检查失败")
            except subprocess.TimeoutExpired:
                result["unity"] = {"status": "fail", "reason": "Unity 编译检查超时", "log": str(unity_log)}
                result["issues"].append("Unity batchmode 编译检查超时")
    result["passed"] = not result["issues"]
    (log_dir / "deterministic-checks.json").write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    return result


def approve_manual_review(config: dict[str, Any], batch_id: str, note: str) -> dict[str, Any]:
    state = read_json(state_path(config))
    if not state or state.get("status") != "manual_review":
        raise SchedulerError("当前没有等待人工验收的 Batch")
    if state.get("current_batch") != batch_id:
        raise SchedulerError(f"当前等待验收的是 Batch {state.get('current_batch')}")
    batch_state = state["batches"][batch_id]
    worktree = Path(batch_state["worktree"])
    progress_path = worktree / config["project"]["progress_file"]
    update_progress(
        progress_path,
        batch_id,
        "completed",
        "自动检查通过；人工验收通过",
        batch_state.get("implementation_commit", "本批实现提交"),
        note,
    )
    tracking_hash = commit_all(worktree, f"docs: 记录 Batch {batch_id} 人工验收")
    fast_forward(Path(state["integration_worktree"]), batch_state["branch"])
    batch_state.update(
        {
            "status": "completed",
            "manual_note": note,
            "tracking_commit": tracking_hash,
            "completed_at": timestamp(),
        }
    )
    state["status"] = "stopped"
    state["current_batch"] = None
    state["completed_this_run"] = int(state.get("completed_this_run", 0)) + 1
    state["updated_at"] = timestamp()
    state["message"] = f"Batch {batch_id} 人工验收通过；可使用 resume 继续"
    write_json(state_path(config), state)
    return state


def request_stop(config: dict[str, Any]) -> Path:
    path = stop_path(config)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(f"requested_at={timestamp()}\n", encoding="utf-8")
    return path


def status_snapshot(config: dict[str, Any]) -> dict[str, Any]:
    state = read_json(state_path(config), {})
    root = project_root(config)
    progress = parse_progress(root / config["project"]["progress_file"])
    return {
        "active_run": state,
        "source_progress": {batch_id: item.status for batch_id, item in progress.items()},
        "stop_requested": stop_path(config).exists(),
    }


def _executor_model(config: dict[str, Any], batch_id: str, attempt: int) -> dict[str, str]:
    complexity = config["batches"][batch_id]["complexity"]
    base = dict(config["models"]["executor_complex" if complexity == "complex" else "executor_default"])
    if attempt > 0:
        efforts = config["models"]["executor_retry_efforts"]
        base["reasoning_effort"] = efforts[min(attempt - 1, len(efforts) - 1)]
    return base


def _reviewer_model(config: dict[str, Any], batch_id: str, attempt: int) -> dict[str, str]:
    complexity = config["batches"][batch_id]["complexity"]
    key = "reviewer_complex" if complexity == "complex" or attempt > 0 else "reviewer_default"
    return dict(config["models"][key])


def _executor_prompt(worktree: Path, task_text: str, attempt: int, feedback: str) -> str:
    rules = (worktree / "automation/prompts/executor.md").read_text(encoding="utf-8")
    retry = ""
    if attempt:
        retry = f"\n## 上一次失败反馈\n\n{feedback}\n"
    return (
        f"{rules}\n\n## 项目路径\n\n{worktree}\n\n"
        f"## 本次任务文档\n\n{task_text}\n{retry}\n"
        "先检查现状，再完成实现和可自动测试；不要处理其他 Batch。"
    )


def _reviewer_prompt(task_text: str, execution: dict[str, Any], deterministic: dict[str, Any]) -> str:
    rules_path = Path(__file__).resolve().parent / "prompts/reviewer.md"
    return (
        f"{rules_path.read_text(encoding='utf-8')}\n\n"
        f"## Batch 文档\n\n{task_text}\n\n"
        f"## 执行 Agent 报告\n\n```json\n{json.dumps(execution, ensure_ascii=False, indent=2)}\n```\n\n"
        f"## 确定性检查\n\n```json\n{json.dumps(deterministic, ensure_ascii=False, indent=2)}\n```\n"
    )


def _record_session(
    config: dict[str, Any],
    state: dict[str, Any],
    batch_state: dict[str, Any],
    role: str,
    result: dict[str, Any],
) -> None:
    record = {"role": role, "recorded_at": timestamp(), **result}
    batch_state["sessions"].append(record)
    state["sessions"].append(record)
    tokens = int(result.get("total_tokens", 0))
    batch_state["tokens"] = int(batch_state.get("tokens", 0)) + tokens
    state["total_tokens"] = int(state.get("total_tokens", 0)) + tokens
    batch_limit = int(config["limits"]["max_batch_tokens"])
    if batch_state["tokens"] > batch_limit:
        batch_state["budget_exceeded"] = True
    state["updated_at"] = timestamp()
    write_json(state_path(config), state)


def _prepare_manual_review(
    config: dict[str, Any],
    state: dict[str, Any],
    batch_id: str,
    execution: dict[str, Any],
    review: dict[str, Any],
    deterministic: dict[str, Any],
) -> str:
    batch_state = state["batches"][batch_id]
    worktree = Path(batch_state["worktree"])
    implementation_hash = _commit_implementation(worktree, execution, batch_id)
    batch_state["implementation_commit"] = implementation_hash
    manual_items = list(review.get("manual_checks", [])) + list(execution.get("manual_checks", []))
    if deterministic.get("unity", {}).get("status") == "manual_required":
        manual_items.append(deterministic["unity"].get("reason", "Unity 需要人工检查"))
    manual_items = list(dict.fromkeys(item for item in manual_items if item))
    detail = "；".join(manual_items) or "该 Batch 配置为必须人工验收"
    update_progress(
        worktree / config["project"]["progress_file"],
        batch_id,
        "blocked",
        "自动检查已完成；等待人工验收",
        implementation_hash,
        detail,
    )
    tracking_hash = commit_all(worktree, f"docs: 记录 Batch {batch_id} 待人工验收")
    checklist_path = _batch_log_dir(config, state, batch_id) / "manual-checklist.md"
    checklist_path.write_text(
        f"# Batch {batch_id} 人工验收清单\n\n"
        + "\n".join(f"- [ ] {item}" for item in manual_items)
        + "\n",
        encoding="utf-8",
    )
    batch_state.update(
        {
            "status": "manual_review",
            "tracking_commit": tracking_hash,
            "manual_checklist": str(checklist_path),
            "review_result": review,
        }
    )
    state["status"] = "manual_review"
    state["message"] = f"Batch {batch_id} 等待人工验收，未合并到集成分支"
    state["updated_at"] = timestamp()
    write_json(state_path(config), state)
    return "manual_review"


def _complete_batch(
    config: dict[str, Any],
    state: dict[str, Any],
    batch_id: str,
    execution: dict[str, Any],
    review: dict[str, Any],
    deterministic: dict[str, Any],
    detail: str,
) -> str:
    batch_state = state["batches"][batch_id]
    worktree = Path(batch_state["worktree"])
    implementation_hash = _commit_implementation(worktree, execution, batch_id)
    update_progress(
        worktree / config["project"]["progress_file"],
        batch_id,
        "completed",
        "自动测试与独立验收通过",
        implementation_hash,
        detail,
    )
    tracking_hash = commit_all(worktree, f"docs: 记录 Batch {batch_id} 验收结果")
    fast_forward(Path(state["integration_worktree"]), batch_state["branch"])
    batch_state.update(
        {
            "status": "completed",
            "implementation_commit": implementation_hash,
            "tracking_commit": tracking_hash,
            "completed_at": timestamp(),
            "review_result": review,
            "deterministic_checks": deterministic,
        }
    )
    state["current_batch"] = None
    state["updated_at"] = timestamp()
    return "completed"


def _commit_implementation(worktree: Path, execution: dict[str, Any], batch_id: str) -> str:
    if changed_files(worktree):
        message = execution.get("suggested_commit_message") or f"feat: 完成 Batch {batch_id}"
        return commit_all(worktree, message)
    return f"无需实现提交（基线 {git_output(worktree, 'rev-parse', '--short', 'HEAD')}）"


def _block_batch(config: dict[str, Any], state: dict[str, Any], batch_id: str, reason: str) -> str:
    batch_state = state["batches"][batch_id]
    batch_state.update({"status": "blocked", "reason": reason, "completed_at": timestamp()})
    integration = Path(state["integration_worktree"])
    progress_path = integration / config["project"]["progress_file"]
    update_progress(
        progress_path,
        batch_id,
        "blocked",
        "自动执行或验收未通过",
        "未合并实现提交",
        reason,
    )
    batch_state["blocked_progress_commit"] = commit_all(
        integration, f"docs: 记录 Batch {batch_id} 自动执行阻塞"
    )
    state["status"] = "blocked"
    state["message"] = f"Batch {batch_id} 在重试上限内未通过：{reason}"
    state["updated_at"] = timestamp()
    write_json(state_path(config), state)
    _write_final_report(config, state)
    return "blocked"


def _set_batch_state(
    config: dict[str, Any], state: dict[str, Any], batch_id: str, status: str, reason: str
) -> str:
    state["batches"][batch_id]["status"] = status
    state["status"] = status
    state["message"] = reason
    state["updated_at"] = timestamp()
    write_json(state_path(config), state)
    return status


def _finish_state(config: dict[str, Any], state: dict[str, Any], status: str, message: str) -> dict[str, Any]:
    state["status"] = status
    state["message"] = message
    state["updated_at"] = timestamp()
    write_json(state_path(config), state)
    if status in {"completed", "blocked"}:
        _write_final_report(config, state)
    return state


def _write_final_report(config: dict[str, Any], state: dict[str, Any]) -> Path:
    path = Path(config["paths"]["logs"]) / state["run_id"] / "final-report.md"
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = [
        f"# 自动调度报告 {state['run_id']}",
        "",
        f"- 状态：{state.get('status')}",
        f"- 开始：{state.get('started_at')}",
        f"- 更新：{state.get('updated_at')}",
        f"- Token：{state.get('total_tokens', 0)}",
        f"- 说明：{state.get('message', '')}",
        "",
        "## Batch",
        "",
    ]
    for batch_id, item in sorted(state.get("batches", {}).items()):
        lines.append(
            f"- Batch {batch_id}: {item.get('status')}，尝试 {item.get('attempts', 0)}，"
            f"Token {item.get('tokens', 0)}"
        )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    state["final_report"] = str(path)
    return path


def _batch_log_dir(config: dict[str, Any], state: dict[str, Any], batch_id: str) -> Path:
    return Path(config["paths"]["logs"]) / state["run_id"] / f"batch-{batch_id}"


def _session_failure_text(session: dict[str, Any], result: dict[str, Any]) -> str:
    return (
        f"Codex exit={session.get('exit_code')}, timeout={session.get('timed_out')}, "
        f"status={result.get('status')}, summary={result.get('summary', '')}"
    )
