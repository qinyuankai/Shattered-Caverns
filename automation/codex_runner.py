from __future__ import annotations

import json
import subprocess
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any

from automation.common import resolve_executable


@dataclass
class SessionResult:
    exit_code: int
    timed_out: bool
    stopped: bool
    duration_seconds: float
    model: str
    reasoning_effort: str
    input_tokens: int
    cached_input_tokens: int
    output_tokens: int
    reasoning_output_tokens: int
    jsonl_log: str
    stderr_log: str
    result_file: str

    @property
    def total_tokens(self) -> int:
        return self.input_tokens + self.output_tokens

    def to_dict(self) -> dict[str, Any]:
        value = asdict(self)
        value["total_tokens"] = self.total_tokens
        return value


def build_codex_command(
    executable: str,
    cwd: Path,
    model: str,
    reasoning_effort: str,
    sandbox: str,
    approval_policy: str,
    schema_path: Path | None,
    result_path: Path,
    ephemeral: bool = True,
) -> list[str]:
    command = [executable, "-a", approval_policy, "exec"]
    if ephemeral:
        command.append("--ephemeral")
    command.extend(
        [
            "--json",
            "--color",
            "never",
            "-s",
            sandbox,
            "-m",
            model,
            "-c",
            f'model_reasoning_effort="{reasoning_effort}"',
            "-C",
            str(cwd),
            "--output-last-message",
            str(result_path),
        ]
    )
    if schema_path is not None:
        command.extend(["--output-schema", str(schema_path)])
    command.append("-")
    return command


def run_codex_session(
    config: dict[str, Any],
    cwd: Path,
    prompt: str,
    model: str,
    reasoning_effort: str,
    sandbox: str,
    schema_path: Path | None,
    log_directory: Path,
    session_name: str,
    timeout_seconds: int,
    stop_file: Path,
) -> SessionResult:
    executable = resolve_executable(config["codex"]["executable"])
    if executable is None:
        raise RuntimeError(f"Codex CLI not found: {config['codex']['executable']}")
    log_directory.mkdir(parents=True, exist_ok=True)
    jsonl_path = log_directory / f"{session_name}.jsonl"
    stderr_path = log_directory / f"{session_name}.stderr.log"
    result_path = log_directory / f"{session_name}.result.json"
    command = build_codex_command(
        executable=executable,
        cwd=cwd,
        model=model,
        reasoning_effort=reasoning_effort,
        sandbox=sandbox,
        approval_policy=config["codex"]["approval_policy"],
        schema_path=schema_path,
        result_path=result_path,
        ephemeral=config["codex"].get("ephemeral", True),
    )

    start = time.monotonic()
    timed_out = False
    stopped = False
    with jsonl_path.open("w", encoding="utf-8") as stdout_handle, stderr_path.open(
        "w", encoding="utf-8"
    ) as stderr_handle:
        process = subprocess.Popen(
            command,
            cwd=cwd,
            text=True,
            stdin=subprocess.PIPE,
            stdout=stdout_handle,
            stderr=stderr_handle,
        )
        assert process.stdin is not None
        process.stdin.write(prompt)
        process.stdin.close()
        while process.poll() is None:
            elapsed = time.monotonic() - start
            if stop_file.exists():
                stopped = True
                _terminate(process, int(config["limits"]["stop_grace_seconds"]))
                break
            if elapsed >= timeout_seconds:
                timed_out = True
                _terminate(process, int(config["limits"]["stop_grace_seconds"]))
                break
            time.sleep(1)
        exit_code = process.wait()

    usage = parse_usage(jsonl_path)
    return SessionResult(
        exit_code=exit_code,
        timed_out=timed_out,
        stopped=stopped,
        duration_seconds=round(time.monotonic() - start, 3),
        model=model,
        reasoning_effort=reasoning_effort,
        input_tokens=usage["input_tokens"],
        cached_input_tokens=usage["cached_input_tokens"],
        output_tokens=usage["output_tokens"],
        reasoning_output_tokens=usage["reasoning_output_tokens"],
        jsonl_log=str(jsonl_path),
        stderr_log=str(stderr_path),
        result_file=str(result_path),
    )


def parse_usage(path: Path) -> dict[str, int]:
    usage = {
        "input_tokens": 0,
        "cached_input_tokens": 0,
        "output_tokens": 0,
        "reasoning_output_tokens": 0,
    }
    if not path.exists():
        return usage
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if event.get("type") != "turn.completed":
            continue
        turn_usage = event.get("usage", {})
        for key in usage:
            usage[key] += int(turn_usage.get(key, 0) or 0)
    return usage


def read_result(path: Path) -> dict[str, Any]:
    if not path.exists():
        return {}
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}


def _terminate(process: subprocess.Popen[str], grace_seconds: int) -> None:
    process.terminate()
    try:
        process.wait(timeout=grace_seconds)
    except subprocess.TimeoutExpired:
        process.kill()
