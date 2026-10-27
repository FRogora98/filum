"""The harness's own logic (spec 043), with no key and no network: python -m unittest discover evals/bench/tests"""

import io
import json
import sys
import tempfile
import unittest
import unittest.mock
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import bench  # noqa: E402
import factconsolidation as fc  # noqa: E402
import longmemeval as lme  # noqa: E402
from backends import parse_stream  # noqa: E402
from common import Journal, bm25_top, ensure_data, mcnemar, stratified  # noqa: E402
from money import Budget, price  # noqa: E402


class DataTests(unittest.TestCase):
    def test_a_download_whose_hash_differs_stops_the_run_and_names_the_file(self):
        with tempfile.TemporaryDirectory() as folder:
            with self.assertRaises(SystemExit) as stop:
                ensure_data("factconsolidation", folder, opener=lambda url: io.BytesIO(b"not the data"))
            self.assertIn("Conflict_Resolution-00000-of-00001.parquet", str(stop.exception))


class MoneyTests(unittest.TestCase):
    def test_a_call_that_could_pass_the_cap_is_refused_and_stops_the_run(self):
        budget = Budget(cap=0.01)
        self.assertIsNotNone(budget.reserve((1.0, 1.0, 1.0), prompt_chars=3_000))
        self.assertIsNone(budget.reserve((100.0, 100.0, 100.0), prompt_chars=300_000))
        self.assertTrue(budget.stopped)

    def test_an_estimate_above_the_cap_refuses_to_start_unless_forced(self):
        args = SimpleNamespace(backend="openai", model="gpt-4o-mini", price_in=None, price_out=None, cap=1.0, force=False)
        many = [(10_000_000, 0)]
        with self.assertRaises(SystemExit):
            bench.check_estimate(args, Budget(1.0), many)
        args.force = True
        bench.check_estimate(args, Budget(1.0), many)

    def test_a_model_with_no_known_price_is_refused(self):
        self.assertIsNone(price("a-model-nobody-priced"))
        args = SimpleNamespace(backend="openai", model="a-model-nobody-priced", judge_model=None, price_in=None, price_out=None,
                               judge_price_in=None, judge_price_out=None)
        with self.assertRaises(SystemExit):
            bench.backends(args, Budget(1.0))


class JournalTests(unittest.TestCase):
    def test_a_resumed_run_skips_what_was_answered_and_counts_what_it_cost(self):
        with tempfile.TemporaryDirectory() as folder:
            first = Journal(Path(folder) / "results.jsonl", lambda r: (r["arm"], r["question_id"]))
            first.add({"arm": "bm25", "question_id": "q1", "cost_usd": 0.25, "judge": {"cost_usd": 0.05}})
            again = Journal(Path(folder) / "results.jsonl", lambda r: (r["arm"], r["question_id"]))
            self.assertEqual({("bm25", "q1")}, again.done())
            self.assertAlmostEqual(0.30, again.spent())


class ProtocolTests(unittest.TestCase):
    def test_bm25_gives_the_best_documents_in_their_own_order(self):
        documents = ["a cat sat", "the dog barked at the cat", "nothing here", "cat cat cat"]
        self.assertEqual([0, 3], bm25_top(documents, "cat sat", 2))

    def test_the_paired_test_gives_the_published_p_values(self):
        self.assertAlmostEqual(0.011, mcnemar(23, 8), places=3)
        self.assertAlmostEqual(0.508, mcnemar(6, 3), places=3)
        self.assertEqual(1.0, mcnemar(0, 0))

    def test_a_selection_is_stratified_and_never_takes_a_used_question(self):
        strata = {"a": [f"a{i}" for i in range(60)], "b": [f"b{i}" for i in range(40)]}
        chosen = stratified(strata, 10, "seed", used={"a1", "a2"})
        self.assertEqual(6, sum(1 for c in chosen if c.startswith("a")))
        self.assertNotIn("a1", chosen)
        self.assertEqual(chosen, stratified(strata, 10, "seed", used={"a1", "a2"}))

    def test_the_substring_metric_is_the_authors(self):
        self.assertTrue(fc.subem("The answer is: Paris.", ["paris"]))
        self.assertTrue(fc.subem("an Apple", ["the apple"]))
        self.assertFalse(fc.subem("London", ["Paris"]))

    def test_facts_are_cut_between_lines_within_the_size(self):
        text = "\n".join(f"{i}. fact number {i}" for i in range(100))
        parts = fc.chunks(text, size=200)
        self.assertTrue(all(len(p) <= 200 for p in parts))
        self.assertEqual(text, "\n".join(parts))

    def test_the_judge_asks_the_authors_prompt_for_each_kind(self):
        item = {"question_id": "q_abs", "question_type": "temporal-reasoning", "question": "Q?", "answer": "A"}
        self.assertIn("unanswerable", lme.judge_prompt(item, "R"))
        item["question_id"] = "q"
        self.assertIn("off-by-one", lme.judge_prompt(item, "R"))

    def test_a_history_is_read_in_date_order_and_cut_between_conversations(self):
        item = {"haystack_dates": ["2023/05/02 (Tue) 10:00", "2023/05/01 (Mon) 09:00"],
                "haystack_sessions": [[{"role": "user", "content": "second"}], [{"role": "user", "content": "first"}]]}
        self.assertEqual(["first", "second"], [t[0]["content"] for _, t in lme.sessions(item)])
        self.assertEqual(1, len(lme.chunks(item)))


class McpBridgeTests(unittest.TestCase):
    """Against a build of this repository (dotnet build), with no key: skipped where there is none."""

    def test_the_bridge_lists_filum_mcps_tools_and_calls_one(self):
        from mcp_client import Mcp

        root = Path(__file__).resolve().parents[3] / "src" / "Filum.Mcp" / "bin"
        builds = sorted(root.glob("*/net10.0/filum-mcp.exe")) + sorted(root.glob("*/net10.0/filum-mcp"))
        if not builds:
            self.skipTest("no filum-mcp build: run dotnet build first")
        with tempfile.TemporaryDirectory() as home, Mcp(builds[0], home, {"FILUM_EMBEDDINGS": "off"}) as mcp:
            names = [f["function"]["name"] for f in mcp.functions()]
            self.assertIn("fact_record", names)
            self.assertIn("events_search", names)
            self.assertIn("Recorded", mcp.tool("fact_record", {"subject": "the person", "attribute": "city", "value": "Lyon"}))
            self.assertIn("Lyon", mcp.tool("facts_current", {"subject": "the person"}))


class CapAndJudgingTests(unittest.TestCase):
    """The cap holds, a stopped build counts what it spent, and a failed judgment is not scored wrong (audit, spec 043)."""

    class FakeClient:
        def __init__(self, text="Paris", fail=False):
            self.calls, self.text, self.fail = [], text, fail
            self.chat = SimpleNamespace(completions=SimpleNamespace(create=self.create))

        def create(self, **kwargs):
            self.calls.append(kwargs)
            if self.fail:
                raise RuntimeError("the judge is down")
            message = SimpleNamespace(content=self.text, tool_calls=None, model_dump=lambda exclude_none=True: {"role": "assistant", "content": self.text})
            usage = SimpleNamespace(prompt_tokens=100, completion_tokens=5, prompt_tokens_details=None)
            return SimpleNamespace(choices=[SimpleNamespace(message=message)], usage=usage)

    def backend(self, client, max_output=None):
        from backends import OpenAiBackend

        with unittest.mock.patch.dict("os.environ", {"OPENAI_API_KEY": "not-a-key"}):
            made = OpenAiBackend("gpt-4o-mini", price("gpt-4o-mini"), Budget(1.0), **({"max_output": max_output} if max_output else {}))
        made.client = client
        return made

    def test_every_call_asks_for_no_more_tokens_than_its_booking(self):
        client = self.FakeClient()
        self.backend(client, max_output=20).ask("Q?")
        self.assertEqual(20, client.calls[0]["max_tokens"])

    def test_a_judge_that_keeps_failing_gives_no_verdict_and_the_answer_is_asked_again(self):
        judge = self.backend(self.FakeClient(fail=True))
        verdict = bench.judge_once(judge, "Is it right?")
        self.assertIsNotNone(verdict.error)
        given = self.backend(self.FakeClient()).ask("Q?")
        row = bench.answer_row("bm25", "q1", "g", "Q?", "Paris", given, {"error": verdict.error}, None, "gpt-4o-mini")
        self.assertIsNone(row["correct"])
        with tempfile.TemporaryDirectory() as folder:
            journal = Journal(Path(folder) / "results.jsonl", lambda r: (r["arm"], r["question_id"]))
            journal.add(row)
            self.assertEqual(set(), journal.done())
            self.assertGreater(journal.spent(), 0)

    def test_a_build_stopped_by_the_cap_is_journaled_with_what_it_spent(self):
        stopped = SimpleNamespace(ask=lambda *a, **k: SimpleNamespace(limited=False, stopped=True, cost_usd=0.02, results=[], seconds=1.0, error=None))
        item = {"question_id": "q1", "haystack_dates": ["2023/05/01 (Mon) 09:00"], "haystack_sessions": [[{"role": "user", "content": "hello"}]]}
        with tempfile.TemporaryDirectory() as folder:
            (Path(folder) / "q1" / "home").mkdir(parents=True)
            entry = lme.ingest(stopped, "filum-mcp", Path(folder), item, {})
        self.assertTrue(entry["retry"])
        self.assertAlmostEqual(0.02, entry["cost_usd"])

    def test_a_turn_with_more_tool_calls_than_the_api_takes_back_is_split(self):
        from backends import MAX_TOOL_CALLS_PER_MESSAGE

        calls = [SimpleNamespace(id=f"c{i}", function=SimpleNamespace(name="fact_record", arguments="{}")) for i in range(MAX_TOOL_CALLS_PER_MESSAGE * 2 + 3)]
        first = SimpleNamespace(content=None, tool_calls=calls, model_dump=lambda exclude_none=True: {"role": "assistant", "tool_calls": [{"id": c.id} for c in calls]})
        client = self.FakeClient()
        replies = iter([first])
        original = client.create

        def create(**kwargs):
            reply = next(replies, None)
            if reply is None:
                return original(**kwargs)
            client.calls.append(kwargs)
            return SimpleNamespace(choices=[SimpleNamespace(message=reply)], usage=SimpleNamespace(prompt_tokens=10, completion_tokens=5, prompt_tokens_details=None))

        client.chat.completions.create = create
        fake_mcp = SimpleNamespace(instructions="", functions=lambda: [], tool=lambda name, args: "Recorded", close=lambda: None)
        with unittest.mock.patch("backends.Mcp", lambda *a: fake_mcp):
            answer = self.backend(client).ask("Save these.", filum=("exe", "home", {}))
        self.assertIsNone(answer.error)
        sent = client.calls[-1]["messages"]
        self.assertTrue(all(len(m.get("tool_calls", [])) <= MAX_TOOL_CALLS_PER_MESSAGE for m in sent))
        self.assertEqual(len(calls), sum(1 for m in sent if m["role"] == "tool"))

    def test_judgments_are_estimated_at_the_judges_price(self):
        args = SimpleNamespace(backend="openai", model="gpt-4o-mini", judge_model="gpt-4o", price_in=None, price_out=None,
                               judge_price_in=None, judge_price_out=None, cap=1.0, force=False)
        bench.check_estimate(args, Budget(1.0), [], [(100_000, 0)])  # $0.25 at gpt-4o
        with self.assertRaises(SystemExit):
            bench.check_estimate(args, Budget(1.0), [], [(500_000, 0)])  # $1.25 at gpt-4o, $0.075 at gpt-4o-mini

    def test_another_endpoint_needs_a_named_judge(self):
        args = SimpleNamespace(backend="openai", model="gpt-4o-mini", judge_model=None, price_in=None, price_out=None,
                               judge_price_in=None, judge_price_out=None)
        with unittest.mock.patch.dict("os.environ", {"OPENAI_BASE_URL": "http://localhost:1/v1"}), self.assertRaises(SystemExit):
            bench.backends(args, Budget(1.0), judged=True)


class KnowsYouTests(unittest.TestCase):
    """Spec 050: the frozen data, the run's shape with a fake backend, and the hooks of a real build."""

    def test_the_frozen_data_has_eight_sessions_twenty_questions_and_three_rules(self):
        import knows_you

        data = knows_you.load()
        self.assertEqual((8, 20, 3), (len(data["sessions"]), len(data["questions"]), len(data["rule_checks"])))
        self.assertEqual(23, len(knows_you.probes(data)))

    def test_a_run_plays_every_session_and_asks_every_probe_of_every_arm(self):
        import threading

        import knows_you
        from backends import Answer

        class Fake:
            model, label = "fake", "usd"

            def __init__(self):
                self.calls = []

            def ask(self, prompt, filum=None, max_turns=None, max_output=None, context=None):
                self.calls.append((prompt, filum is not None, context))
                return Answer(text="yes", cost_usd=0.001)

        days = []

        class FakeHooks:
            def brief(self, home, env=None):
                days.append((env or {}).get("FILUM_TODAY"))
                return "the brief"

            def recall(self, home, message, env=None):
                return "the recall"

            def tidy(self, home, session, env=None):
                return ""

        answer, judge = Fake(), Fake()
        with tempfile.TemporaryDirectory() as folder:
            journal = Journal(Path(folder) / "results.jsonl", lambda r: (r["arm"], r["question_id"]))
            played = Journal(Path(folder) / "sessions.jsonl", lambda r: (r["arm"], r["step"]))
            finished = knows_you.run(knows_you.load(), knows_you.ARMS, answer, judge, "filum-mcp", FakeHooks(), Path(folder), journal, played,
                                     bench.answer_row, bench.judge_once, threading.Lock(), log=lambda *_: None)
            self.assertTrue(finished)
            self.assertEqual(23 * 3, len(journal.rows))
            self.assertEqual(8 * 2 + 23 * 3, len(answer.calls))
            self.assertTrue(all(c[2] == "the brief\n\nthe recall" for c in answer.calls if c[1] and c[2] is not None))
            self.assertEqual(8 + 23, sum(1 for c in answer.calls if c[2] is not None))
            self.assertTrue(all(r["correct"] is True for r in journal.rows))
            # Today is each session's day, then the probe day (spec 055).
            data = knows_you.load()
            self.assertEqual([s["date"] for s in data["sessions"]] + [data["probe_date"]] * 23, days)

    def test_the_hooks_of_a_real_build_give_the_brief_and_the_recall(self):
        import knows_you

        root = Path(__file__).resolve().parents[3] / "src" / "Filum.Mcp" / "bin"
        builds = sorted(root.glob("*/net10.0/filum-mcp.exe")) + sorted(root.glob("*/net10.0/filum-mcp"))
        if not builds:
            self.skipTest("no filum-mcp build: run dotnet build first")
        with tempfile.TemporaryDirectory() as home:
            hooks = knows_you.Hooks(builds[0])
            self.assertEqual("", hooks.recall(home, "We moved the piano to the attic last week."))
            self.assertIn("attic", hooks.recall(home, "Where is the piano now?"))
            self.assertIn("messages kept", hooks.brief(home))
            metrics = hooks.metrics()
            self.assertEqual(3, metrics["hook_calls"])
            self.assertGreater(metrics["brief_chars"], 0)
            self.assertIn("brief --home", hooks.claude_settings(home)["hooks"]["SessionStart"][0]["hooks"][0]["command"])


class StreamTests(unittest.TestCase):
    def test_a_claude_code_stream_gives_the_texts_the_tools_and_the_cost(self):
        stream = "\n".join(json.dumps(x) for x in [
            {"type": "assistant", "message": {"content": [{"type": "tool_use", "id": "t1", "name": "mcp__filum__fact_record"}]}},
            {"type": "user", "message": {"content": [{"type": "tool_result", "tool_use_id": "t1", "content": "Refused: no"}]}},
            {"type": "assistant", "message": {"content": [{"type": "text", "text": "Done."}]}},
            {"type": "result", "total_cost_usd": 0.01, "usage": {"input_tokens": 10, "output_tokens": 2}},
        ])
        answer = parse_stream(stream)
        self.assertEqual(("Done.", ["fact_record"], 0.01), (answer.text, answer.tools, answer.cost_usd))
        self.assertIn("Refused", answer.results[0][1])

    def test_the_hooks_claude_code_ran_give_their_context_and_time(self):
        brief = json.dumps({"hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": "You are Noor."}})
        stream = "\n".join(json.dumps(x) for x in [
            {"type": "system", "subtype": "hook_started", "hook_id": "h1", "hook_event": "SessionStart"},
            {"type": "system", "subtype": "hook_response", "hook_id": "h1", "hook_event": "SessionStart", "stdout": brief},
            {"type": "system", "subtype": "hook_started", "hook_id": "h2", "hook_event": "Stop"},
            {"type": "system", "subtype": "hook_response", "hook_id": "h2", "hook_event": "Stop", "stdout": json.dumps({"decision": "block", "reason": "Tidy."})},
            {"type": "result", "total_cost_usd": 0.0, "usage": {}},
        ])
        answer = parse_stream(stream, [1.0, 1.4, 2.0, 2.1, 3.0])
        import knows_you

        hooks = knows_you.Hooks("filum-mcp")
        context = hooks.observe(answer)
        self.assertEqual(("You are Noor.", 1, [13]), (context, hooks.tidy_asked, hooks.brief_chars))
        self.assertAlmostEqual(0.4, hooks.seconds[0])



class IsolationTests(unittest.TestCase):
    def test_a_run_sees_the_login_but_nothing_of_the_person(self):
        import time

        import backends

        with tempfile.TemporaryDirectory() as home:
            Path(home, ".claude").mkdir()
            Path(home, ".claude", ".credentials.json").write_text(json.dumps(
                {"claudeAiOauth": {"accessToken": "t", "expiresAt": (time.time() + 7200) * 1000}, "mcpOAuth": {"x": {}}}), encoding="utf-8")
            config = backends.IsolatedConfig("claude", "m", home)
            config.started = True
            Path(config.directory, ".claude.json").write_text(json.dumps({"oauthAccount": {"emailAddress": "a@example.invalid", "accountUuid": "u"}}), encoding="utf-8")
            try:
                env = config.env()
                account = json.loads(Path(config.directory, ".claude.json").read_text(encoding="utf-8"))["oauthAccount"]
                login = json.loads(Path(config.directory, ".credentials.json").read_text(encoding="utf-8"))
            finally:
                config.close()
        self.assertEqual(("", "u", ["claudeAiOauth"]), (account["emailAddress"], account["accountUuid"], list(login)))
        self.assertEqual(("1", "1", str(config.directory)), (env["CLAUDE_CODE_DISABLE_AUTO_MEMORY"], env["CLAUDE_CODE_DISABLE_CLAUDE_MDS"], env["CLAUDE_CONFIG_DIR"]))
        self.assertNotIn("ANTHROPIC_API_KEY", env)


    def test_with_a_setup_token_the_users_login_is_never_copied(self):
        import tempfile
        import time

        import backends

        with tempfile.TemporaryDirectory() as home:
            Path(home, ".claude").mkdir()
            Path(home, ".claude", ".credentials.json").write_text(json.dumps({"claudeAiOauth": {"expiresAt": (time.time() + 60) * 1000}}), encoding="utf-8")
            config = backends.IsolatedConfig("claude", "m", home)
            config.started = True
            try:
                with unittest.mock.patch.dict("os.environ", {"CLAUDE_CODE_OAUTH_TOKEN": "t"}), unittest.mock.patch("subprocess.run") as run:
                    env = config.env()
                    copied = Path(config.directory, ".credentials.json").exists()
            finally:
                config.close()
        self.assertEqual(("t", False), (env["CLAUDE_CODE_OAUTH_TOKEN"], copied))
        run.assert_not_called()


if __name__ == "__main__":
    unittest.main()
