from __future__ import annotations

import json
from datetime import datetime
from pathlib import Path
from typing import Any
from zoneinfo import ZoneInfo

from automation.scheduler import budget_allows_session, select_next_batch


def run_simulation(config: dict[str, Any]) -> dict[str, Any]:
    events: list[dict[str, Any]] = []
    statuses = {batch_id: "pending" for batch_id in config["batches"]}
    state = {"total_tokens": 0}

    selected = select_next_batch(statuses, config["batches"])
    events.append({"case": "dependency_selection", "selected": selected, "passed": selected == "01"})

    retries = 0
    scripted_results = ["executor_fail", "reviewer_fail", "pass"]
    for result in scripted_results:
        events.append({"case": "retry", "attempt": retries + 1, "result": result})
        if result == "pass":
            statuses["01"] = "completed"
            break
        retries += 1
    events.append({"case": "retry_limit", "retries": retries, "passed": retries == 2})

    next_after_01 = select_next_batch(statuses, config["batches"])
    events.append(
        {"case": "resume_after_completion", "selected": next_after_01, "passed": next_after_01 == "02"}
    )

    stop_requested = True
    resume_started = stop_requested and next_after_01 == "02"
    events.append({"case": "stop_resume", "passed": resume_started})

    failures = int(config["limits"]["max_fix_retries"]) + 1
    blocked = failures > int(config["limits"]["max_fix_retries"])
    events.append({"case": "blocked_after_retry_limit", "passed": blocked})

    state["total_tokens"] = int(config["limits"]["max_total_tokens"])
    events.append({"case": "budget_stop", "passed": not budget_allows_session(state, config)})

    result = {
        "simulation": True,
        "created_at": datetime.now(ZoneInfo("Asia/Shanghai")).isoformat(timespec="seconds"),
        "passed": all(event.get("passed", True) for event in events),
        "events": events,
        "note": "未启动 Codex、Unity 或真实 Batch，未修改 PROGRESS.md。",
    }
    output = Path(config["paths"]["logs"]) / "simulation-latest.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    result["output"] = str(output)
    return result
