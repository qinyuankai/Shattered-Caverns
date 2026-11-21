from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from automation.codex_runner import run_codex_session
from automation.common import load_config, project_root
from automation.git_ops import preflight
from automation.scheduler import (
    SchedulerError,
    approve_manual_review,
    request_stop,
    resume_run,
    start_run,
    state_path,
    status_snapshot,
    stop_path,
)
from automation.simulator import run_simulation
from automation.validate import validate_configuration


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Shattered Caverns 多 Batch 自动调度器")
    parser.add_argument("--max-batches", type=int, help="本次最多执行多少个 Batch")
    subparsers = parser.add_subparsers(dest="command")
    subparsers.add_parser("status", help="查看当前状态")
    subparsers.add_parser("stop", help="请求安全停止")
    resume = subparsers.add_parser("resume", help="从中断处恢复")
    resume.add_argument("--max-batches", type=int, dest="resume_max_batches")
    approve = subparsers.add_parser("approve", help="确认当前 Batch 的人工验收")
    approve.add_argument("batch_id")
    approve.add_argument("--note", required=True, help="人工验收证据或说明")
    subparsers.add_parser("preflight", help="仅运行真实任务前安全检查")
    subparsers.add_parser("validate", help="检查配置、Batch、Unity 与模型目录")
    subparsers.add_parser("simulate", help="运行不调用模型、不修改项目的状态机模拟")
    subparsers.add_parser("probe", help="启动一个最小独立 Codex 会话")
    subparsers.add_parser("self-test", help="运行调度器单元测试")
    subparsers.add_parser("report", help="显示最近最终报告路径")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    config = load_config()
    command = args.command or "run"
    try:
        if command == "run":
            result = start_run(config, args.max_batches)
        elif command == "resume":
            result = resume_run(config, args.resume_max_batches)
        elif command == "status":
            result = status_snapshot(config)
        elif command == "stop":
            result = {"stop_file": str(request_stop(config)), "message": "已请求安全停止"}
        elif command == "approve":
            result = approve_manual_review(config, args.batch_id.zfill(2), args.note)
        elif command == "preflight":
            flight = preflight(config)
            result = {"ok": flight.ok, "checks": flight.checks, "errors": flight.errors}
            print(json.dumps(result, ensure_ascii=False, indent=2))
            return 0 if flight.ok else 2
        elif command == "validate":
            checks, errors = validate_configuration(config)
            result = {"ok": not errors, "checks": checks, "errors": errors}
            print(json.dumps(result, ensure_ascii=False, indent=2))
            return 0 if not errors else 1
        elif command == "simulate":
            result = run_simulation(config)
        elif command == "probe":
            result = run_probe(config)
        elif command == "self-test":
            return subprocess.call(
                [sys.executable, "-m", "unittest", "discover", "-s", "automation/tests", "-v"],
                cwd=project_root(config),
            )
        elif command == "report":
            state = status_snapshot(config)["active_run"]
            result = {"final_report": state.get("final_report"), "status": state.get("status")}
        else:
            raise SchedulerError(f"未知命令：{command}")
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except SchedulerError as exc:
        print(f"安全停止：{exc}", file=sys.stderr)
        return 2


def run_probe(config: dict) -> dict:
    stamp = datetime.now(ZoneInfo("Asia/Shanghai")).strftime("%Y%m%d-%H%M%S")
    log_dir = Path(config["paths"]["logs"]) / f"probe-{stamp}"
    model = config["models"]["executor_default"]
    stop_path(config).unlink(missing_ok=True)
    session = run_codex_session(
        config=config,
        cwd=project_root(config),
        prompt="Reply exactly MODEL_PROBE_OK. Do not use tools.",
        model=model["model"],
        reasoning_effort=model["reasoning_effort"],
        sandbox="read-only",
        schema_path=None,
        log_directory=log_dir,
        session_name="independent-session",
        timeout_seconds=120,
        stop_file=stop_path(config),
    )
    result_text = Path(session.result_file).read_text(encoding="utf-8").strip() if Path(session.result_file).exists() else ""
    return {
        "passed": session.exit_code == 0 and result_text == "MODEL_PROBE_OK",
        "response": result_text,
        "session": session.to_dict(),
    }


if __name__ == "__main__":
    raise SystemExit(main())
