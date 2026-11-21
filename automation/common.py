from __future__ import annotations

import json
import os
import shutil
import subprocess
from pathlib import Path
from typing import Any

import yaml


AUTOMATION_DIR = Path(__file__).resolve().parent
CONFIG_PATH = AUTOMATION_DIR / "config.yaml"


def load_config(path: Path = CONFIG_PATH) -> dict[str, Any]:
    with path.open(encoding="utf-8") as handle:
        config = yaml.safe_load(handle)
    root = (path.parent / config["project"]["root"]).resolve()
    config["_config_path"] = str(path.resolve())
    config["_project_root"] = str(root)
    for key in ("logs", "runtime", "worktree_parent"):
        config["paths"][key] = str((root / config["paths"][key]).resolve())
    return config


def project_root(config: dict[str, Any]) -> Path:
    return Path(config["_project_root"])


def resolve_executable(name: str) -> str | None:
    if Path(name).is_file():
        return str(Path(name).resolve())
    return shutil.which(name)


def run_command(
    args: list[str],
    cwd: Path,
    timeout: int = 30,
    check: bool = False,
) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        args,
        cwd=cwd,
        text=True,
        capture_output=True,
        timeout=timeout,
        check=check,
        env=os.environ.copy(),
    )


def read_json(path: Path, default: Any = None) -> Any:
    if not path.exists():
        return default
    with path.open(encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".tmp")
    with temporary.open("w", encoding="utf-8") as handle:
        json.dump(value, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    temporary.replace(path)
