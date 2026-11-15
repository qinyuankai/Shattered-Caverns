from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from automation.codex_runner import build_codex_command, parse_usage
from automation.common import load_config
from automation.progress import parse_progress, update_progress
from automation.scheduler import (
    budget_allows_session,
    is_unity_environment_failure,
    request_stop,
    select_next_batch,
    stop_path,
)


class ProgressTests(unittest.TestCase):
    def test_parse_and_update_progress(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "PROGRESS.md"
            path.write_text(
                "| Batch | 任务 | 状态 | 测试结果 | 提交记录 |\n"
                "|---|---|---|---|---|\n"
                "| 01 | 第一批 | pending | 未测试 | 无 |\n",
                encoding="utf-8",
            )
            self.assertEqual(parse_progress(path)["01"].status, "pending")
            update_progress(path, "01", "completed", "测试通过", "abc123", "模拟验收")
            updated = parse_progress(path)["01"]
            self.assertEqual(updated.status, "completed")
            self.assertEqual(updated.commit_record, "abc123")
            self.assertIn("模拟验收", path.read_text(encoding="utf-8"))


class SelectionTests(unittest.TestCase):
    def setUp(self) -> None:
        self.batches = {
            "01": {"depends_on": []},
            "02": {"depends_on": ["01"]},
            "03": {"depends_on": ["01"]},
        }

    def test_selects_first_ready_batch(self) -> None:
        statuses = {"01": "pending", "02": "pending", "03": "pending"}
        self.assertEqual(select_next_batch(statuses, self.batches), "01")

    def test_resumes_without_repeating_completed_batch(self) -> None:
        statuses = {"01": "completed", "02": "pending", "03": "pending"}
        self.assertEqual(select_next_batch(statuses, self.batches), "02")

    def test_blocked_dependency_stops_selection(self) -> None:
        statuses = {"01": "blocked", "02": "pending", "03": "pending"}
        self.assertIsNone(select_next_batch(statuses, self.batches))


class BudgetAndCodexTests(unittest.TestCase):
    def test_budget_reserve(self) -> None:
        config = load_config()
        maximum = config["limits"]["max_total_tokens"]
        reserve = config["limits"]["minimum_remaining_tokens_to_start_session"]
        self.assertTrue(budget_allows_session({"total_tokens": maximum - reserve}, config))
        self.assertFalse(budget_allows_session({"total_tokens": maximum - reserve + 1}, config))

    def test_parse_codex_usage(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "session.jsonl"
            events = [
                {"type": "turn.started"},
                {
                    "type": "turn.completed",
                    "usage": {
                        "input_tokens": 100,
                        "cached_input_tokens": 50,
                        "output_tokens": 20,
                        "reasoning_output_tokens": 5,
                    },
                },
            ]
            path.write_text("\n".join(json.dumps(item) for item in events), encoding="utf-8")
            usage = parse_usage(path)
            self.assertEqual(usage["input_tokens"], 100)
            self.assertEqual(usage["output_tokens"], 20)

    def test_codex_command_uses_safe_noninteractive_flags(self) -> None:
        command = build_codex_command(
            executable="codex",
            cwd=Path("/tmp/project"),
            model="gpt-5.6-sol",
            reasoning_effort="low",
            sandbox="read-only",
            approval_policy="never",
            schema_path=None,
            result_path=Path("/tmp/result.json"),
        )
        self.assertEqual(command[:4], ["codex", "-a", "never", "exec"])
        self.assertIn("--ephemeral", command)
        self.assertIn("read-only", command)
        self.assertNotIn("--dangerously-bypass-approvals-and-sandbox", command)

    def test_stop_file_can_be_created_and_cleared_for_resume(self) -> None:
        config = load_config()
        with tempfile.TemporaryDirectory() as directory:
            config["paths"]["runtime"] = directory
            created = request_stop(config)
            self.assertTrue(created.exists())
            stop_path(config).unlink()
            self.assertFalse(stop_path(config).exists())

    def test_unity_licensing_failure_requires_manual_review(self) -> None:
        log = "[Licensing::Module] IPC channel to LicensingClient doesn't exist; aborting"
        self.assertTrue(is_unity_environment_failure(log, ""))
        self.assertFalse(is_unity_environment_failure("Scripts have compiler errors", ""))


class ConfigurationTests(unittest.TestCase):
    def test_all_14_batches_are_configured(self) -> None:
        config = load_config()
        self.assertEqual(sorted(config["batches"]), [f"{value:02d}" for value in range(1, 15)])
        self.assertEqual(config["limits"]["max_fix_retries"], 2)
        self.assertFalse(config["git"]["push"])
        self.assertFalse(config["git"]["merge_main"])


if __name__ == "__main__":
    unittest.main()
