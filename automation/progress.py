from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from zoneinfo import ZoneInfo


ALLOWED_STATUSES = {"pending", "in_progress", "completed", "blocked"}
ROW_PATTERN = re.compile(
    r"^\|\s*(?P<batch>\d{2})\s*\|(?P<task>[^|]*)\|\s*"
    r"(?P<status>pending|in_progress|completed|blocked)\s*\|"
    r"(?P<test>[^|]*)\|(?P<commit>[^|]*)\|\s*$"
)


@dataclass(frozen=True)
class BatchProgress:
    batch_id: str
    task: str
    status: str
    test_result: str
    commit_record: str


def parse_progress(path: Path) -> dict[str, BatchProgress]:
    batches: dict[str, BatchProgress] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = ROW_PATTERN.match(line)
        if not match:
            continue
        values = {key: value.strip() for key, value in match.groupdict().items()}
        batches[values["batch"]] = BatchProgress(
            batch_id=values["batch"],
            task=values["task"],
            status=values["status"],
            test_result=values["test"],
            commit_record=values["commit"],
        )
    return batches


def update_progress(
    path: Path,
    batch_id: str,
    status: str,
    test_result: str,
    commit_record: str,
    detail: str,
) -> None:
    if status not in ALLOWED_STATUSES:
        raise ValueError(f"Unsupported status: {status}")
    lines = path.read_text(encoding="utf-8").splitlines()
    replaced = False
    for index, line in enumerate(lines):
        match = ROW_PATTERN.match(line)
        if match and match.group("batch") == batch_id:
            task = match.group("task").strip()
            lines[index] = (
                f"| {batch_id} | {task} | {status} | "
                f"{_table_text(test_result)} | {_table_text(commit_record)} |"
            )
            replaced = True
            break
    if not replaced:
        raise KeyError(f"Batch {batch_id} not found in {path}")

    stamp = datetime.now(ZoneInfo("Asia/Shanghai")).strftime("%Y-%m-%d %H:%M:%S %Z")
    lines.extend(
        [
            "",
            f"### 自动调度记录 Batch {batch_id} · {stamp}",
            "",
            f"- 状态：{status}",
            f"- 测试结果：{test_result}",
            f"- 提交记录：{commit_record}",
            f"- 说明：{detail}",
        ]
    )
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def _table_text(value: str) -> str:
    return " ".join(value.replace("|", "／").split())
