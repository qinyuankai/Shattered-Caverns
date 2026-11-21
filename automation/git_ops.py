from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from automation.common import run_command


@dataclass
class PreflightResult:
    ok: bool
    checks: list[str]
    errors: list[str]


def git_output(root: Path, *args: str, timeout: int = 30) -> str:
    result = run_command(["git", *args], cwd=root, timeout=timeout)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip())
    return result.stdout.strip()


def preflight(config: dict[str, Any]) -> PreflightResult:
    root = Path(config["_project_root"])
    checks: list[str] = []
    errors: list[str] = []
    try:
        top = Path(git_output(root, "rev-parse", "--show-toplevel")).resolve()
        if top != root.resolve():
            errors.append(f"项目根目录不匹配：{top}")
        else:
            checks.append(f"Git 根目录：{top}")
    except RuntimeError as exc:
        return PreflightResult(False, checks, [f"Git 仓库检查失败：{exc}"])

    branch = git_output(root, "branch", "--show-current")
    required = config["project"]["required_branch"]
    if branch != required:
        errors.append(f"当前分支为 {branch}，要求 {required}")
    else:
        checks.append(f"当前分支：{branch}")

    status = git_output(root, "status", "--porcelain")
    if config["validation"]["require_clean_source_worktree"] and status:
        errors.append("源工作区存在未提交修改；调度器不会自动 stash、reset 或覆盖，请先人工收口基线")
    else:
        checks.append("源工作区干净")

    required_paths = [
        config["project"]["plan_file"],
        config["project"]["progress_file"],
        config["project"]["tasks_directory"],
        "automation",
        "AGENTS.md",
    ]
    for relative in required_paths:
        if not (root / relative).exists():
            errors.append(f"缺少必需路径：{relative}")

    if config["validation"]["require_tracked_automation_files"]:
        untracked = git_output(root, "ls-files", "--others", "--exclude-standard", "--", "PLAN.md", "PROGRESS.md", "tasks", "automation")
        if untracked:
            errors.append("计划或自动化文件尚未纳入 Git；独立 worktree 无法读取它们")
        else:
            checks.append("计划与自动化文件已纳入 Git")
    return PreflightResult(not errors, checks, errors)


def create_integration_worktree(
    root: Path, worktree_parent: Path, run_id: str, branch_prefix: str
) -> tuple[Path, str]:
    branch = f"{branch_prefix}/{run_id}"
    path = worktree_parent / run_id / "integration"
    path.parent.mkdir(parents=True, exist_ok=True)
    _ensure_safe_new_path(path, worktree_parent)
    git_output(root, "worktree", "add", "-b", branch, str(path), "HEAD", timeout=120)
    return path, branch


def create_batch_worktree(
    root: Path,
    worktree_parent: Path,
    run_id: str,
    batch_id: str,
    branch_prefix: str,
    integration_branch: str,
) -> tuple[Path, str]:
    branch = f"{branch_prefix}-{batch_id}/{run_id}"
    path = worktree_parent / run_id / f"batch-{batch_id}"
    path.parent.mkdir(parents=True, exist_ok=True)
    _ensure_safe_new_path(path, worktree_parent)
    git_output(root, "worktree", "add", "-b", branch, str(path), integration_branch, timeout=120)
    return path, branch


def changed_files(worktree: Path) -> list[str]:
    result = run_command(["git", "status", "--porcelain"], cwd=worktree, timeout=30)
    if result.returncode != 0:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip())
    output = result.stdout
    files: list[str] = []
    for line in output.splitlines():
        if not line:
            continue
        value = line[3:]
        if " -> " in value:
            value = value.split(" -> ", 1)[1]
        files.append(value)
    return files


def check_disallowed_paths(paths: list[str], disallowed: list[str]) -> list[str]:
    return [path for path in paths if any(path == prefix.rstrip("/") or path.startswith(prefix) for prefix in disallowed)]


def commit_all(worktree: Path, message: str) -> str:
    git_output(worktree, "add", "--all")
    git_output(worktree, "commit", "-m", message, timeout=120)
    return git_output(worktree, "rev-parse", "--short", "HEAD")


def fast_forward(integration_worktree: Path, batch_branch: str) -> None:
    git_output(integration_worktree, "merge", "--ff-only", batch_branch, timeout=120)


def _ensure_safe_new_path(path: Path, parent: Path) -> None:
    resolved_parent = parent.resolve()
    resolved_path = path.resolve()
    if resolved_path == resolved_parent or resolved_parent not in resolved_path.parents:
        raise RuntimeError(f"Unsafe worktree path: {resolved_path}")
    if path.exists():
        raise RuntimeError(f"Worktree path already exists: {path}")
    if not re.fullmatch(r"[A-Za-z0-9._/-]+", path.name):
        raise RuntimeError(f"Unexpected worktree name: {path.name}")
