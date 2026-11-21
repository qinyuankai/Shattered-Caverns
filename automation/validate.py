from __future__ import annotations

import re
import sys
from pathlib import Path
from typing import Any

if __package__ in {None, ""}:
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from automation.common import load_config, project_root, resolve_executable, run_command
from automation.progress import parse_progress


def validate_configuration(config: dict[str, Any]) -> tuple[list[str], list[str]]:
    root = project_root(config)
    checks: list[str] = []
    errors: list[str] = []

    version_file = root / "ProjectSettings/ProjectVersion.txt"
    if not version_file.exists():
        errors.append("缺少 ProjectSettings/ProjectVersion.txt")
    else:
        match = re.search(r"m_EditorVersion:\s*(\S+)", version_file.read_text(encoding="utf-8"))
        actual = match.group(1) if match else "unknown"
        expected = config["project"]["unity_version"]
        if actual != expected:
            errors.append(f"Unity 版本不匹配：配置 {expected}，项目 {actual}")
        else:
            checks.append(f"Unity 项目版本：{actual}")

    unity_path = Path(config["project"]["unity_executable"])
    if unity_path.is_file():
        checks.append(f"Unity Editor：{unity_path}")
    else:
        errors.append(f"Unity Editor 不存在：{unity_path}")

    progress_path = root / config["project"]["progress_file"]
    progress = parse_progress(progress_path)
    configured = set(config["batches"])
    documented = set(progress)
    if configured != documented:
        errors.append(f"Batch 集合不一致：配置 {sorted(configured)}，进度 {sorted(documented)}")
    else:
        checks.append(f"Batch 数量：{len(configured)}")

    task_directory = root / config["project"]["tasks_directory"]
    for batch_id, details in config["batches"].items():
        task = task_directory / f"batch-{batch_id}.md"
        if not task.exists():
            errors.append(f"缺少任务文档：{task.relative_to(root)}")
        for dependency in details.get("depends_on", []):
            if dependency not in configured:
                errors.append(f"Batch {batch_id} 依赖未知 Batch {dependency}")
            if dependency >= batch_id:
                errors.append(f"Batch {batch_id} 的依赖 {dependency} 不是前序任务")
    if not errors:
        checks.append("任务文件与依赖关系完整")

    codex = resolve_executable(config["codex"]["executable"])
    if codex is None:
        errors.append("未找到 Codex CLI")
    else:
        version = run_command([codex, "--version"], cwd=root, timeout=30)
        if version.returncode != 0:
            errors.append(f"Codex CLI 无法运行：{version.stderr.strip()}")
        else:
            checks.append(f"Codex CLI：{version.stdout.strip()}")
        catalog = run_command([codex, "debug", "models"], cwd=root, timeout=60)
        if catalog.returncode != 0:
            errors.append(f"无法读取 Codex 模型目录：{catalog.stderr.strip()}")
        else:
            requested = {
                value["model"]
                for key, value in config["models"].items()
                if isinstance(value, dict) and "model" in value and value.get("enabled", True)
            }
            missing = sorted(model for model in requested if model not in catalog.stdout)
            if missing:
                errors.append(f"模型目录中未找到：{', '.join(missing)}")
            else:
                checks.append(f"已验证模型：{', '.join(sorted(requested))}")

    allowed_efforts = {"low", "medium", "high", "xhigh", "max", "ultra"}
    for key, value in config["models"].items():
        if isinstance(value, dict) and "reasoning_effort" in value:
            if value["reasoning_effort"] not in allowed_efforts:
                errors.append(f"无效推理强度 {key}: {value['reasoning_effort']}")
    for effort in config["models"]["executor_retry_efforts"]:
        if effort not in allowed_efforts:
            errors.append(f"无效重试推理强度：{effort}")
    if config["limits"].get("max_cost_usd") is not None:
        errors.append("当前 Codex CLI 不返回可靠价格，max_cost_usd 必须为 null；请使用 Token 上限")
    return checks, errors


def main() -> int:
    checks, errors = validate_configuration(load_config())
    for item in checks:
        print(f"PASS  {item}")
    for item in errors:
        print(f"FAIL  {item}")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
