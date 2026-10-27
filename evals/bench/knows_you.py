"""It knows who you are (spec 050): a synthetic person told over eight sessions, then a fresh session with no hints.

Arms: "filum-hooks" (filum-mcp, plus the start brief and the recall on every message, spec 045), "filum" (filum-mcp
alone) and "none" (no memory). The data is frozen in data/knows-you.json."""

import json
import os
import re
import statistics
import subprocess
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
DATA = HERE / "data" / "knows-you.json"
DATA_2 = HERE / "data" / "knows-you-2.json"
DATA_3 = HERE / "data" / "knows-you-3.json"
DATA_4 = HERE / "data" / "knows-you-4.json"
ARMS = ("filum-hooks", "filum", "none")
SESSION_TURNS = 30

# Capture apart from recall (spec 046): the word that shows a question's answer was written in the memory, and reached
# the model. An analysis aid written for the Track A round, after the data was frozen; it changes no question and no
# expected answer. None: nothing to capture (abstention, synthesis).
EVIDENCE_1 = {
    "name": "Sam", "job": "nurse", "city": "Grenoble", "past-city": "Lyon", "sister-city": "Braga", "sister-name": "Ana",
    "dog": "Pixel", "dog-health": "surgery", "diet": "vegetarian", "dislike": "cilantro", "race-date": "14",
    "run-days": "Tuesday", "shifts": "night", "promise": "photos", "passport": "passport", "trip": "Braga",
    "first-race": "first", "abstain-movie": None, "abstain-car": None, "plan-week": None,
    "short-bullets": "bullet", "short-bullets-2": "bullet", "no-meat": "vegetarian",
}
# The held-out person's, written with its data and before any answer on it was seen.
EVIDENCE_2 = {
    "name": "Noor", "job": "chemistry", "new-job": "Rotterdam", "city": "Utrecht", "partner-job": "lighting", "cats": "Ravioli",
    "third-cat": "third", "cat-health": "tooth", "lessons": "Thursday", "exam": "N4", "allergy": "peanut",
    "mother-city": "Amersfoort", "mother-gift": "pottery", "promise-movers": "moving", "promise-slides": "slides",
    "to-do-move": "municipality", "past-lessons": "Wednesday", "abstain-car": None, "abstain-sibling": None,
    "plan-summer": None, "metric": "metric", "english-in-dutch": "English", "metric-2": "metric",
}
# The third person's, written with its data on 2026-10-19, after the second round's fixes and before any answer on it.
EVIDENCE_3 = {
    "name": "Kofi", "job": "structural", "promotion": "Manchester", "city": "Leeds", "daughter-age": None, "family": "Efua",
    "dog": "dog", "rehearsals": "Tuesday", "concert": "concert", "brother": "Kwame", "dairy": "lactose", "manager": "Helen",
    "promise-cottage": "cottage", "promise-drive": "Haddad", "membership": "chartered", "past-rehearsals": "Monday",
    "abstain-car": None, "abstain-sister": None, "efua-football": "regional", "plan-may": None, "short": "short",
    "24h": "24-hour", "no-dairy": "lactose",
}
# The fourth person's (spec 055), written with its data on 2026-10-19, before any run on it.
EVIDENCE_4 = {
    "name": "Ines", "job": "architect", "work-now": "Lisbon", "work-september": "Lisbon", "son-age": None, "family": "Tiago",
    "climbing": "Friday", "past-climbing": "Tuesday", "competition": "third", "restaurant": "Maré", "allergy": "shellfish",
    "reading-list": "Piranesi", "photos-sent": "photos", "promise": "photos", "to-do-trip": "passport", "sister": "Rita",
    "abstain-brother": None, "abstain-blood": None, "swimming": "twice", "plan-summer": None, "celsius": "Celsius",
    "no-emoji": "emoji", "summary-first": "summary",
}
EVIDENCE = {**EVIDENCE_1}
# An answer that says it does not know: on something that was said and written, a false "I do not know" (spec 055).
NOT_KNOWN = re.compile(r"\b(?:I )?(?:don't|do not) (?:know|have (?:any )?(?:information|a record|that))|no record of|not (?:sure|recorded|in (?:my|your) memory)|never (?:mentioned|said|told)", re.I)
# False only to see what setting today changes (spec 055): then the engine takes the machine's date.
SET_TODAY = True
WRITE_TOOLS = {"memory_write", "memory_edit", "memory_append", "fact_record", "fact_end", "collection_add_rows", "collection_update_rows"}


def use(data_path):
    """Loads a person and points the evidence at theirs."""
    data = load(data_path)
    EVIDENCE.clear()
    EVIDENCE.update({DATA_2.name: EVIDENCE_2, DATA_3.name: EVIDENCE_3, DATA_4.name: EVIDENCE_4}.get(Path(data_path).name, EVIDENCE_1))
    return data


def memory_text(home):
    """Every file of a memory folder, as text (the history folder left out)."""
    home = Path(home)
    if not home.exists():
        return ""
    return "\n".join(f.read_text(encoding="utf-8", errors="replace") for f in home.rglob("*") if f.is_file() and ".filum" not in f.parts)


def trace(key, home, context, answer):
    """Was the evidence written (captured), and did it reach the model (in the hook context or a tool's result)?"""
    word = EVIDENCE.get(key)
    if word is None:
        return {}
    seen = (context or "") + "\n".join(text for _, text in answer.results)
    return {"captured": word.lower() in memory_text(home).lower(), "in_context": word.lower() in seen.lower()}

JUDGE_ANSWER = ("I will give you a question a person asked their personal assistant, the answer the assistant should give "
                "from what the person told it earlier, and the assistant's response. Answer yes if the response gives the "
                "expected answer (other true details are fine; for an unknown, it must say it does not know). Otherwise "
                "answer no.\n\nQuestion: {0}\n\nExpected answer: {1}\n\nResponse: {2}\n\nDoes the response give the "
                "expected answer? Answer yes or no only.")
JUDGE_RULE = ("I will give you a request a person made to their personal assistant, a rule the assistant should follow "
              "from what the person told it earlier, and the assistant's response. Answer yes if the response follows "
              "the rule, otherwise no.\n\nRequest: {0}\n\nRule: {1}\n\nResponse: {2}\n\nDoes the response follow the "
              "rule? Answer yes or no only.")


def load(path=DATA):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def session_prompt(session):
    return f"Today is {session['date']}.\n\n" + "\n\n".join(session["messages"])


def probes(data):
    """(id, kind, prompt, judge prompt maker) for every question and rule check, asked on the probe day."""
    day = f"Today is {data['probe_date']}. "
    items = [(q["id"], q["kind"], day + q["question"], lambda r, q=q: JUDGE_ANSWER.format(q["question"], q["expected"], r)) for q in data["questions"]]
    items += [(c["id"], "rule", day + c["prompt"], lambda r, c=c: JUDGE_RULE.format(c["prompt"], c["rule"], r)) for c in data["rule_checks"]]
    return items


class Hooks:
    """Filum's hooks run as Claude Code runs them: `filum-mcp brief` at a session's start, `filum-mcp log` on every message.
    Each call is timed, and what it gives the model is measured."""

    def __init__(self, exe):
        self.exe, self.seconds, self.brief_chars, self.recall_chars = str(exe), [], [], []
        self.tidy_asked, self.tidy_cost, self.tidy_seconds, self.tidy_tokens = 0, 0.0, 0.0, 0

    def _run(self, command, home, stdin, field=("hookSpecificOutput", "additionalContext"), env=None):
        started = time.monotonic()
        done = subprocess.run([self.exe, command, "--home", str(home)], input=stdin, capture_output=True, text=True, encoding="utf-8", timeout=60,
                              env={**os.environ, **(env or {})})
        self.seconds.append(time.monotonic() - started)
        if not done.stdout.strip():
            return ""
        answer = json.loads(done.stdout)
        for key in field:
            answer = answer[key]
        return answer

    def tidy(self, home, session, env=None):
        """The Stop hook: the reason to tidy when much is waiting, once per session; empty otherwise."""
        reason = self._run("tidy", home, json.dumps({"session_id": session, "hook_event_name": "Stop", "stop_hook_active": False}), field=("reason",), env=env)
        self.tidy_asked += 1 if reason else 0
        return reason

    def brief(self, home, env=None):
        text = self._run("brief", home, json.dumps({"hook_event_name": "SessionStart", "source": "startup"}), env=env)
        self.brief_chars.append(len(text))
        return text

    def recall(self, home, message, env=None):
        text = self._run("log", home, json.dumps({"hook_event_name": "UserPromptSubmit", "prompt": message}), env=env)
        self.recall_chars.append(len(text))
        return text

    def observe(self, answer):
        """What Claude Code's own hook runs gave the model, as its stream-json reports them; the context for `trace`."""
        given = []
        for event, stdout, seconds in answer.hooks:
            if seconds is not None:
                self.seconds.append(seconds)
            try:
                output = json.loads(stdout) if stdout.strip() else {}
            except json.JSONDecodeError:
                output = {}
            if event == "Stop":
                self.tidy_asked += 1 if output.get("decision") == "block" else 0
                continue
            text = (output.get("hookSpecificOutput") or {}).get("additionalContext") or ""
            (self.brief_chars if event == "SessionStart" else self.recall_chars).append(len(text))
            given.append(text)
        return "\n\n".join(t for t in given if t)

    def claude_settings(self, home):
        """The same hooks as Claude Code project settings."""
        hook = lambda command: [{"hooks": [{"type": "command", "command": f'"{self.exe}" {command} --home "{home}"'}]}]  # noqa: E731
        return {"hooks": {"SessionStart": hook("brief"), "UserPromptSubmit": hook("log"), "Stop": hook("tidy")}}

    def metrics(self):
        def pct(values, p):
            return round(statistics.quantiles(values, n=100)[p - 1], 3) if len(values) >= 2 else (round(values[0], 3) if values else None)
        return {"brief_chars": max(self.brief_chars, default=0),
                "recall_chars_mean": round(statistics.mean(self.recall_chars), 1) if self.recall_chars else 0,
                "recall_chars_max": max(self.recall_chars, default=0),
                "hook_seconds_p50": pct(self.seconds, 50), "hook_seconds_p95": pct(self.seconds, 95), "hook_calls": len(self.seconds),
                "tidy_asked": self.tidy_asked, "tidy_usd": round(self.tidy_cost, 4), "tidy_seconds": round(self.tidy_seconds, 1), "tidy_input_tokens": self.tidy_tokens}


def ask(backend, arm, prompt, filum, hooks, home):
    """One session's message, as each arm gives it to the model."""
    if arm != "filum-hooks" or hooks is None:
        return backend.ask(prompt, filum=filum, max_turns=SESSION_TURNS)
    if hasattr(backend, "ask_with_hooks"):
        given = backend.ask_with_hooks(prompt, filum=filum, settings=hooks.claude_settings(home), max_turns=SESSION_TURNS)
        given.context = hooks.observe(given)
        return given
    env = filum[2] if filum else None
    brief, recall = hooks.brief(home, env), hooks.recall(home, prompt, env)
    context = "\n\n".join(t for t in (brief, recall) if t)
    given = backend.ask(prompt, filum=filum, max_turns=SESSION_TURNS, context=context or None)
    given.context = context
    return given


def today(day):
    return {"FILUM_TODAY": day} if SET_TODAY else {}


def note_writes(answer, written, tidying=False):
    """Which path first wrote each piece of evidence, from the write calls' arguments in order: the turn's own tools, or
    consolidation (after a memory_consolidate call, or in the tidy the Stop hook asked for) (spec 046)."""
    consolidating = tidying
    for name, arguments in answer.calls:
        if name == "memory_consolidate":
            consolidating = True
        elif name in WRITE_TOOLS:
            for word in {w for w in EVIDENCE.values() if w}:
                if word not in written and word.lower() in arguments.lower():
                    written[word] = "consolidation" if consolidating else "turn"


def stop(backend, arm, filum, hooks, home, session):
    """Claude Code's Stop hook, as the API backend plays it: when filum-mcp tidy asks, the model gets its reason as the
    next message, with the memory's tools. None when it did not ask."""
    if arm != "filum-hooks" or hooks is None or hasattr(backend, "ask_with_hooks"):
        return None
    reason = hooks.tidy(home, session, filum[2] if filum else None)
    if not reason:
        return None
    started = time.monotonic()
    tidied = backend.ask(reason, filum=filum, max_turns=SESSION_TURNS)
    hooks.tidy_seconds += time.monotonic() - started
    hooks.tidy_cost += tidied.cost_usd
    hooks.tidy_tokens += tidied.input_tokens
    return tidied


def run(data, arms, answer, judge, exe, hooks, work, journal, played, row, judge_once, lock, log=print):
    """Plays the sessions of every arm, then each probe in a fresh session on a copy of the memory. False when a limit
    or the cap stopped it. `played` journals each arm's sessions; `row` makes a result line."""
    import shutil

    for arm in arms:
        home = work / arm / "home"
        written = next((r.get("written_by", {}) for r in played.rows if r.get("arm") == arm and r.get("step") == "sessions" and not r.get("retry")), {})
        filum = None if arm == "none" else (exe, home, {"FILUM_EMBEDDINGS": "off"})
        if arm != "none" and (arm, "sessions") not in played.done():
            shutil.rmtree(home, ignore_errors=True)
            home.mkdir(parents=True)
            spent = 0.0
            for number, session in enumerate(data["sessions"], 1):
                filum = (exe, home, {**filum[2], **today(session["date"])})
                given = ask(answer, arm, session_prompt(session), filum, hooks, home)
                spent += given.cost_usd
                if given.limited or given.stopped:
                    played.add({"arm": arm, "step": "sessions", "cost_usd": spent, "retry": True})
                    return False
                note_writes(given, written)
                tidied = stop(answer, arm, filum, hooks, home, f"{arm}-session-{number}")
                if tidied is not None:
                    spent += tidied.cost_usd
                    note_writes(tidied, written, tidying=True)
            played.add({"arm": arm, "step": "sessions", "cost_usd": spent, "written_by": written})
            log(f"[{arm}] {len(data['sessions'])} sessions played")
        for key, kind, prompt, judging in probes(data):
            if (arm, key) in journal.done():
                continue
            copy = work / arm / "probes" / key
            shutil.rmtree(copy, ignore_errors=True)
            if filum:
                shutil.copytree(home, copy)
            captured_before = trace(key, copy, "", type("NoResults", (), {"results": []})()) if filum else {}
            given = ask(answer, arm, prompt, None if filum is None else (exe, copy, {**filum[2], **today(data["probe_date"])}), hooks, copy)
            if given.limited or given.stopped:
                return False
            seen = trace(key, copy, getattr(given, "context", ""), given)
            seen = {**seen, "captured": captured_before.get("captured", False),
                    "written_by": written.get(EVIDENCE.get(key) or "", "not written" if not captured_before.get("captured") else "unknown")} if seen else seen
            seen["false_unknown"] = bool(seen.get("captured") and kind != "abstention" and EVIDENCE.get(key) and NOT_KNOWN.search(given.text or ""))
            seen["overview_waiting"] = any(n == "memory_overview" and "# Said and not tidied yet" in t for n, t in given.results)
            verdict = None if given.error or not given.text else judge_once(judge, judging(given.text))
            if not given.error and given.text and verdict is None:
                return False
            correct = False if verdict is None and not given.error else None if verdict is None or verdict.error else "yes" in verdict.text.lower()
            judged = None if verdict is None else {"answer": verdict.text, "error": verdict.error, "cost_usd": round(verdict.cost_usd, 6), "model": judge.model}
            with lock:
                journal.add({**row(arm, key, kind, prompt, None, given, judged, correct, answer.model), **seen})
            log(f"[{arm}] {key}: {'error' if given.error else 'not judged' if correct is None else 'correct' if correct else 'wrong'}")
    return True
