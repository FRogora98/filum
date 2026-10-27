"""The report of a run (spec 043): each arm's accuracy, by ability or hop, the exact McNemar test of each pair of arms on
the questions both answered, and the cost."""

import itertools
import json
from pathlib import Path

from common import mcnemar


def load(out):
    path = Path(out) / "results.jsonl"
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines() if path.exists() else []:
        if line.strip():
            r = json.loads(line)
            rows[(r["arm"], r["question_id"])] = r  # the last answer of a question counts
    return list(rows.values())


def markdown(rows, group, title, money_label, ingestion=0.0):
    arms = list(dict.fromkeys(r["arm"] for r in rows))
    groups = sorted({r[group] for r in rows})
    lines = [f"# {title}", "", f"| Arm | All | {' | '.join(groups)} | Not judged | Tool calls (mean) | {money_label} |",
             "|---|---|" + "---|" * len(groups) + "---|---|---|"]
    for arm in arms:
        of = [r for r in rows if r["arm"] == arm]
        judged = [r for r in of if r["correct"] is not None]
        by = [f"{sum(r['correct'] for r in judged if r[group] == g)}/{sum(1 for r in judged if r[group] == g)}" for g in groups]
        spent = sum(r["cost_usd"] + (r.get("judge") or {}).get("cost_usd", 0) for r in of)
        lines.append(f"| {arm} | {sum(r['correct'] for r in judged)}/{len(judged)} | {' | '.join(by)} | {len(of) - len(judged)} | "
                     f"{sum(r['tool_calls'] for r in of) / max(1, len(of)):.1f} | {spent:.4f} |")
    lines += ["", "\"Not judged\": an answer that failed or a judge that kept failing; a resume asks it again. The counts are of judged answers.",
              "", "Paired on the questions both arms answered and had judged (exact McNemar, two-sided):", ""]
    for first, second in itertools.combinations(arms, 2):
        a = {r["question_id"]: r["correct"] for r in rows if r["arm"] == first and r["correct"] is not None}
        b = {r["question_id"]: r["correct"] for r in rows if r["arm"] == second and r["correct"] is not None}
        both = a.keys() & b.keys()
        only_a = sum(1 for q in both if a[q] and not b[q])
        only_b = sum(1 for q in both if b[q] and not a[q])
        lines.append(f"- {first} vs {second}: {sum(a[q] for q in both)} vs {sum(b[q] for q in both)} of {len(both)}; "
                     f"only {first} {only_a}, only {second} {only_b}; p = {mcnemar(only_a, only_b):.3g}")
    models = sorted({(r.get("model"), (r.get("judge") or {}).get("model")) for r in rows}, key=str)
    lines += ["", f"Models (answer, judge): {models}", f"Saving the facts cost {ingestion:.4f} {money_label}; the table counts the answers and judgments."]
    return "\n".join(lines) + "\n"


def write(out, group, title, money_label, ingestion=0.0):
    rows = load(out)
    text = markdown(rows, group, title, money_label, ingestion) if rows else f"# {title}\n\nNo answers yet.\n"
    (Path(out) / "report.md").write_text(text, encoding="utf-8")
    return text
