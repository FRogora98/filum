"""Filum on public memory benchmarks, with your own key, in one command (spec 043). Run by hand, never in CI.

  python evals/bench/bench.py longmemeval --model gpt-4o-mini --count 20 --cap 5
  python evals/bench/bench.py factconsolidation --length 64k --model gpt-4o-mini --cap 5
  python evals/bench/bench.py knows-you --model gpt-4o-mini --cap 2
  python evals/bench/bench.py report --out <dir> [--title <title>]

Every step runs and resumes by itself: the data (pinned, checked by SHA-256), filum-mcp (built from this repository
unless --filum-mcp), the memories, the answers, the judgments and the report. See evals/bench/README.md."""

import argparse
import concurrent.futures as cf
import json
import os
import re
import shutil
import subprocess
import sys
import threading
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import factconsolidation as fc  # noqa: E402
import knows_you  # noqa: E402
import longmemeval as lme  # noqa: E402
import report  # noqa: E402
from backends import ClaudeCodeBackend, OpenAiBackend  # noqa: E402
from common import Journal, ensure_data  # noqa: E402
from money import Budget, estimate, price  # noqa: E402

REPO = HERE.parents[1]
OFFICIAL_JUDGE = "gpt-4o-2024-08-06"  # LongMemEval's own judge
JUDGE_ATTEMPTS = 3
# A verdict is "yes" or "no".
JUDGE_OUTPUT = 20


def models_folder():
    return Path(os.environ.get("FILUM_MODELS") or Path.home() / ".filum" / "models") / "multilingual-e5-small"


def filum_mcp(args, work):
    """The filum-mcp to answer through: --filum-mcp, or one built from this repository once."""
    if args.filum_mcp:
        return Path(args.filum_mcp).resolve()
    target = work / "filum-mcp"
    exe = target / ("filum-mcp.exe" if os.name == "nt" else "filum-mcp")
    if not exe.exists():
        print(f"Building filum-mcp into {target}", flush=True)
        subprocess.run(["dotnet", "publish", str(REPO / "src" / "Filum.Mcp"), "-c", "Release", "-o", str(target), "-v", "q", "-nologo"], check=True)
    return exe


def backends(args, budget, judged=False):
    """(answer backend, judge backend); refuses a model whose price is not known, and, on an endpoint other than OpenAI's,
    a judged benchmark with no judge named (the official one may not be there)."""
    if args.backend == "claude-code":
        return ClaudeCodeBackend(args.model, budget), ClaudeCodeBackend(args.judge_model or args.model, budget)
    if judged and os.environ.get("OPENAI_BASE_URL") and not args.judge_model:
        raise SystemExit(f"OPENAI_BASE_URL is set: name a judge your endpoint serves with --judge-model (the official one is {OFFICIAL_JUDGE}).")
    prices = price(args.model, args.price_in, args.price_out)
    judge_model = args.judge_model or OFFICIAL_JUDGE
    judge_prices = price(judge_model, args.judge_price_in, args.judge_price_out)
    for model, known in ((args.model, prices), (judge_model, judge_prices)):
        if known is None:
            raise SystemExit(f"No price known for {model}: give --price-in/--price-out (or --judge-price-in/--judge-price-out), USD per million tokens.")
    return OpenAiBackend(args.model, prices, budget), OpenAiBackend(judge_model, judge_prices, budget, max_turns=1, max_output=JUDGE_OUTPUT)


def check_estimate(args, budget, calls, judge_calls=()):
    """Refuses to start when the estimate passes the cap, unless --force. Judgments are priced at the judge's price."""
    if args.backend == "claude-code":
        return
    judge = price(getattr(args, "judge_model", None) or OFFICIAL_JUDGE, getattr(args, "judge_price_in", None), getattr(args, "judge_price_out", None))
    dollars = estimate(price(args.model, args.price_in, args.price_out), calls) + (estimate(judge, judge_calls) if judge and judge_calls else 0)
    print(f"Estimate: about ${dollars:.2f} more, ${budget.spent:.2f} already spent, cap ${args.cap:.2f}", flush=True)
    if budget.spent + dollars > args.cap and not args.force:
        raise SystemExit(f"The estimate passes the cap of ${args.cap:.2f}: raise --cap, ask fewer questions, or use --force (the cap still stops the run).")


def play(tasks, parallel, step):
    halt = threading.Event()

    def run(task):
        if not halt.is_set() and step(*task) is False:
            halt.set()

    with cf.ThreadPoolExecutor(max_workers=parallel) as pool:
        for f in cf.as_completed([pool.submit(run, t) for t in tasks]):
            f.result()
    return not halt.is_set()


def answer_row(arm, key, group, item_question, gold, answer, judged, correct, model):
    """A failed answer or judgment is journaled with what it cost, scored as not judged (None), and asked again on a
    resume."""
    failed = bool(answer.error) or correct is None
    return {"arm": arm, "question_id": key, "group": group, "question": item_question, "gold": gold, **answer.row(),
            "correct": None if failed else correct, "model": model, "judge": judged, **({"retry": True} if failed else {})}


def judge_once(judge, prompt):
    """The verdict, after up to JUDGE_ATTEMPTS tries; None when the run must stop (a limit or the cap)."""
    verdict = None
    for _ in range(JUDGE_ATTEMPTS):
        verdict = judge.ask(prompt)
        if verdict.limited or verdict.stopped:
            return None
        if not verdict.error:
            break
    return verdict


def run_longmemeval(args):
    out, work = Path(args.out).resolve(), Path(args.work).resolve()
    work.mkdir(parents=True, exist_ok=True)
    arms = args.arms.split(",")
    journal, ingested = Journal(out / "results.jsonl", lambda r: (r["arm"], r["question_id"])), Journal(work / "ingest.jsonl", lambda r: r["question_id"])
    budget = Budget(args.cap, journal.spent() + ingested.spent())
    # The key and the prices are checked before anything is downloaded.
    answer, judge = backends(args, budget, judged=True)
    items = {i["question_id"]: i for i in lme.load(ensure_data("longmemeval_s", args.data))}
    ids = json.loads(Path(args.ids).read_text(encoding="utf-8")) if args.ids else lme.select(items.values(), args.count, args.seed)
    out.mkdir(parents=True, exist_ok=True)
    (out / "ids.json").write_text(json.dumps(ids, indent=2) + "\n", encoding="utf-8")
    todo = [(a, q) for q in ids for a in arms if (a, q) not in journal.done()]
    expected = [lme.calls(items[q], [a for a, p in todo if p == q]) for q in {q for _, q in todo}]
    check_estimate(args, budget, [c for a, _ in expected for c in a], [c for _, j in expected for c in j])
    exe, env, lock = None, {"FILUM_TOOLS": args.tools} if args.tools else {}, threading.Lock()
    if any(a.startswith("filum") for a in arms):
        exe = filum_mcp(args, work)
        missing = [q for q in ids if not (work / q / "home").exists()]
        if missing:
            (work / "preload-ids.json").write_text(json.dumps(missing), encoding="utf-8")
            subprocess.run(["dotnet", "run", "-c", "Release", "--project", str(REPO / "src" / "Filum.Evals"), "--", "preload", "--data", str(ensure_data("longmemeval_s", args.data)),
                            "--ids", str(work / "preload-ids.json"), "--work", str(work), "--embeddings", str(models_folder())], check=True)
    if "filum-facts" in arms:
        def build(q):
            entry = lme.ingest(answer, exe, work, items[q], env)
            with lock:
                ingested.add(entry)
            print(json.dumps(entry), flush=True)
            if entry.get("retry"):
                print(f"Stopped while building {q}: a limit or the cap (what it spent is counted).", flush=True)
                return False
        if not play([(q,) for q in ids if q not in ingested.done()], args.parallel, build):
            return report.write(out, "group", "LongMemEval_S on Filum", answer.label, ingested.spent())

    def ask(arm, q):
        item = items[q]
        filum = None
        if arm.startswith("filum"):
            home = work / q / arm / "home"
            shutil.rmtree(home, ignore_errors=True)
            shutil.copytree(work / q / ("home-facts" if arm == "filum-facts" else "home"), home)
            filum = (exe, home, env)
        given = answer.ask(lme.prompt(arm, item), filum=filum)
        if given.limited or given.stopped:
            return False
        verdict = None
        if not given.error and given.text:
            verdict = judge_once(judge, lme.judge_prompt(item, given.text))
            if verdict is None:
                return False
        # No text is a wrong answer; a judge that kept failing is no verdict.
        correct = False if verdict is None and not given.error else None if verdict is None or verdict.error else "yes" in verdict.text.lower()
        judged = None if verdict is None else {"answer": verdict.text, "error": verdict.error, "cost_usd": round(verdict.cost_usd, 6), "model": judge.model}
        with lock:
            journal.add(answer_row(arm, q, lme.stratum(item), item["question"], item["answer"], given, judged, correct, answer.model))
        print(f"[{arm}] {q}: {'error' if given.error else 'not judged' if correct is None else 'correct' if correct else 'wrong'} · {len(given.tools)} tools · ${budget.spent:.2f}", flush=True)

    play(todo, args.parallel, ask)
    return report.write(out, "group", "LongMemEval_S on Filum", answer.label, ingested.spent())


def run_factconsolidation(args):
    out, work = Path(args.out).resolve(), Path(args.work).resolve() / f"fc-{args.length}"
    work.mkdir(parents=True, exist_ok=True)
    # The key and the prices are checked before anything is downloaded.
    backends(args, Budget(args.cap))
    data = fc.rows(ensure_data("factconsolidation", args.data), args.length)
    ids = json.loads(Path(args.ids).read_text(encoding="utf-8")) if args.ids else fc.select(data, args.per_hop, args.seed)
    out.mkdir(parents=True, exist_ok=True)
    (out / "ids.json").write_text(json.dumps(ids, indent=2) + "\n", encoding="utf-8")
    arms, history = args.arms.split(","), fc.context(data)
    journal, log = Journal(out / "results.jsonl", lambda r: (r["arm"], r["question_id"])), Journal(work / "ingest.jsonl", lambda r: r.get("done"))
    budget = Budget(args.cap, journal.spent() + log.spent())
    answer, _ = backends(args, budget)
    todo = [(a, h, i) for h in ids for i in ids[h] for a in arms if (a, f"{h}-{i}") not in journal.done()]
    check_estimate(args, budget, fc.calls(args.length, arms, len(todo) // max(1, len(arms)), ingest="filum-facts" in arms and True not in log.done()))
    exe, env, lock = None, {"FILUM_TOOLS": args.tools} if args.tools else {}, threading.Lock()
    if "filum-facts" in arms:
        exe, home = filum_mcp(args, Path(args.work).resolve()), work / "home"
        if True not in log.done():
            shutil.rmtree(home, ignore_errors=True)
            home.mkdir(parents=True)
            if not fc.ingest(answer, exe, home, history, env, log.add):
                print("Stopped while saving the facts: a limit, the cap, a refused fact or a chunk left short (see ingest.jsonl).", flush=True)
                return report.write(out, "group", f"FactConsolidation {args.length} on Filum", answer.label, log.spent())
            log.add({"done": True})

    def ask(arm, hop, i):
        question, golds = data[hop]["questions"][i], [str(a) for a in data[hop]["answers"][i]]
        filum = None
        if arm.startswith("filum"):
            copy = work / "answers" / f"{hop}-{i}"
            shutil.rmtree(copy, ignore_errors=True)
            shutil.copytree(work / "home", copy)
            filum = (exe, copy, env)
        given = answer.ask(fc.prompt(arm, question, history), filum=filum)
        if filum:
            shutil.rmtree(filum[1], ignore_errors=True)
        if given.limited or given.stopped:
            return False
        correct = not given.error and fc.subem(given.text, golds)
        with lock:
            journal.add(answer_row(arm, f"{hop}-{i}", hop, question, golds, given, None, correct, answer.model))
        print(f"[{arm}] {hop}-{i}: {'error' if given.error else 'correct' if correct else 'wrong'} · ${budget.spent:.2f}", flush=True)

    play(todo, args.parallel, ask)
    return report.write(out, "group", f"FactConsolidation {args.length} on Filum", answer.label, log.spent())


def run_knows_you(args):
    out, work = Path(args.out).resolve(), Path(args.work).resolve()
    work.mkdir(parents=True, exist_ok=True)
    data, arms = knows_you.use(args.data_file), args.arms.split(",")
    knows_you.SET_TODAY = not args.no_today
    journal, played = Journal(out / "results.jsonl", lambda r: (r["arm"], r["question_id"])), Journal(work / "sessions.jsonl", lambda r: (r["arm"], r["step"]))
    budget = Budget(args.cap, journal.spent() + played.spent())
    answer, judge = backends(args, budget, judged=True)
    if args.backend == "claude-code" and not (isolated := check_isolation(args)).startswith("isolated"):
        return isolated
    probes = len(knows_you.probes(data))
    # Each session and each probe reads the instructions, the tools and the brief again, about 15,000 tokens a turn,
    # for a few turns; each judgment about 600 tokens.
    check_estimate(args, budget, [(45_000, 1_000)] * (len(data["sessions"]) * sum(a != "none" for a in arms) + probes * len(arms)),
                   [(600, 5)] * probes * len(arms))
    exe = filum_mcp(args, work) if any(a != "none" for a in arms) else None
    hooks = knows_you.Hooks(exe) if "filum-hooks" in arms else None
    finished = knows_you.run(data, arms, answer, judge, exe, hooks, work, journal, played, answer_row, judge_once, threading.Lock())
    if not finished:
        print("Stopped: a limit or the cap (what it spent is counted).", flush=True)
    text = report.write(out, "group", "It knows who you are (spec 050)", answer.label, played.spent())
    traced = [r for r in report.load(out) if "captured" in r]
    if traced:
        lines = ["", "Capture apart from recall (spec 046), on the probes with something to capture:"]
        for arm in dict.fromkeys(r["arm"] for r in traced):
            of = [r for r in traced if r["arm"] == arm]
            paths = {p: sum(1 for r in of if r.get("written_by") == p) for p in ("turn", "consolidation", "unknown", "not written")}
            lines.append(f"- {arm}: written {sum(r['captured'] for r in of)}/{len(of)} (by the turn {paths['turn']}, by consolidation {paths['consolidation']}, "
                         f"unattributed {paths['unknown']}, not written {paths['not written']}), reached the model {sum(r['in_context'] for r in of)}/{len(of)}, "
                         f"answered right {sum(r['correct'] is True for r in of)}/{len(of)}")
        lines += ["", "What a fresh session's answer costs (every probe):"]
        for arm in dict.fromkeys(r["arm"] for r in report.load(out)):
            of = [r for r in report.load(out) if r["arm"] == arm]
            seconds = sorted(r["seconds"] for r in of)
            p95 = seconds[min(len(seconds) - 1, int(round(0.95 * (len(seconds) - 1))))]
            waited = [r for r in of if r.get("overview_waiting")]
            unknown = sum(1 for r in of if r.get("false_unknown"))
            lines.append(f"- {arm}: {sum(seconds) / len(seconds):.1f} s on average, p95 {p95:.1f} s; {sum(r['input_tokens'] for r in of) / len(of):,.0f} input tokens on average; "
                         f"{sum(r['tool_calls'] for r in of) / len(of):.1f} tool calls; {len(waited)} probes got waiting messages in memory_overview"
                         + (f" ({sum(r['seconds'] for r in waited) / len(waited):.1f} s on average)" if waited else "")
                         + f"; {unknown} false \"I do not know\" on something said and written")
        extra = "\n".join(lines) + "\n"
        with (out / "report.md").open("a", encoding="utf-8") as f:
            f.write(extra)
        text += extra
    if hooks is not None:
        metrics = hooks.metrics()
        (out / "hooks.json").write_text(json.dumps(metrics, indent=2) + "\n", encoding="utf-8")
        extra = (f"\nHooks (filum-hooks): brief {metrics['brief_chars']} characters at most; recall per message {metrics['recall_chars_mean']} "
                 f"on average, {metrics['recall_chars_max']} at most; hook time p50 {metrics['hook_seconds_p50']} s, p95 {metrics['hook_seconds_p95']} s "
                 f"({metrics['hook_calls']} calls).\n")
        with (out / "report.md").open("a", encoding="utf-8") as f:
            f.write(extra)
        text += extra
    return text


def check_isolation(args):
    """Asks `claude -p`, as a run does, who the person is: the model must see nothing of the account running it (spec 050).
    The account's email is read here to look for it in the answer, never printed."""
    import backends as b

    account = json.loads((Path.home() / ".claude.json").read_text(encoding="utf-8")).get("oauthAccount") or {}
    identity = {k: str(account.get(k) or "").strip().lower() for k in b.IDENTITY}
    answer = b.ClaudeCodeBackend(args.model, Budget(1.0)).ask(
        "What is my email address, my name and my organisation? Is a persistent memory directory or a MEMORY.md described "
        "in your instructions? Answer each with NONE when you do not know.")
    leaked = [k for k, v in identity.items() if len(v) > 3 and v in answer.text.lower()]
    leaked += ["memory directory"] if re.search(r"[\\/]\.claude[\\/]|[\\/]memory[\\/]", answer.text) else []
    return "isolated: the model saw nothing of the account" if not leaked and not answer.error else f"NOT isolated: {leaked or answer.error}"


def arguments(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="benchmark", required=True)
    for name, arms in (("longmemeval", lme.ARMS), ("factconsolidation", fc.ARMS)):
        p = sub.add_parser(name)
        p.add_argument("--model", required=True, help="the answer (and ingestion) model")
        p.add_argument("--judge-model", help=f"LongMemEval's judge (default {OFFICIAL_JUDGE}, the official one, with --backend openai)")
        p.add_argument("--backend", choices=("openai", "claude-code"), default="openai")
        p.add_argument("--cap", type=float, required=True, help="USD the run may spend, judge included")
        p.add_argument("--force", action="store_true", help="start even when the estimate passes the cap (the cap still stops the run)")
        p.add_argument("--price-in", type=float), p.add_argument("--price-out", type=float)
        p.add_argument("--judge-price-in", type=float), p.add_argument("--judge-price-out", type=float)
        p.add_argument("--arms", default=",".join(arms))
        p.add_argument("--ids", help="the questions: a JSON list of ids (LongMemEval) or {sh: [...], mh: [...]} (FactConsolidation)")
        p.add_argument("--seed", default="20261014")
        p.add_argument("--out", default=str(REPO / "evals" / "results" / name))
        p.add_argument("--work", default=str(REPO / "evals" / "work" / name))
        p.add_argument("--data", default=str(REPO / "evals" / "data"))
        p.add_argument("--filum-mcp", help="a filum-mcp to answer through (default: built from this repository)")
        p.add_argument("--tools", choices=("core", "full"), help="filum-mcp's tool profile (default: as shipped)")
        p.add_argument("--parallel", type=int, default=2)
        if name == "longmemeval":
            p.add_argument("--count", type=int, default=20, help="questions, stratified by type (ignored with --ids)")
        else:
            p.add_argument("--length", choices=fc.LENGTHS, default="64k")
            p.add_argument("--per-hop", type=int, default=25, help="single-hop and multi-hop questions each (ignored with --ids)")
    k = sub.add_parser("knows-you")
    k.add_argument("--model", required=True, help="the model of every session and answer")
    k.add_argument("--judge-model", help=f"the judge (default {OFFICIAL_JUDGE} with --backend openai)")
    k.add_argument("--backend", choices=("openai", "claude-code"), default="openai")
    k.add_argument("--cap", type=float, required=True, help="USD the run may spend, judge included")
    k.add_argument("--force", action="store_true")
    k.add_argument("--price-in", type=float), k.add_argument("--price-out", type=float)
    k.add_argument("--judge-price-in", type=float), k.add_argument("--judge-price-out", type=float)
    k.add_argument("--arms", default=",".join(knows_you.ARMS))
    k.add_argument("--data-file", default=str(knows_you.DATA), help="the person: data/knows-you.json (default), or data/knows-you-2.json, -3.json or -4.json (held out)")
    k.add_argument("--no-today", action="store_true", help="leave FILUM_TODAY unset: the engine takes the machine's date")
    k.add_argument("--out", default=str(REPO / "evals" / "results" / "knows-you"))
    k.add_argument("--work", default=str(REPO / "evals" / "work" / "knows-you"))
    k.add_argument("--filum-mcp", help="a filum-mcp to answer through (default: built from this repository)")
    i = sub.add_parser("isolation-check", help="checks that claude -p runs see nothing of the account running them")
    i.add_argument("--model", default="claude-haiku-4-5-20251001")
    r = sub.add_parser("report")
    r.add_argument("--out", required=True)
    r.add_argument("--title", default="Filum")
    return parser.parse_args(argv)


def main(argv=None):
    sys.stdout.reconfigure(encoding="utf-8")
    args = arguments(argv)
    if args.benchmark == "report":
        print(report.write(args.out, "group", args.title, "usd"))
        return
    print({"longmemeval": run_longmemeval, "factconsolidation": run_factconsolidation, "knows-you": run_knows_you,
           "isolation-check": check_isolation}[args.benchmark](args))


if __name__ == "__main__":
    main()
