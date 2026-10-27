"""LongMemEval_S (Wu et al., MIT) on Filum (spec 043), as the published rounds ran it.

Arms: "filum-facts" (every turn an event, plus the facts an agent saved from the history), "filum" (the events only) and
"bm25" (the five sessions BM25 ranks first, in the prompt). The judge asks the authors' prompts (evaluate_qa.py)."""

import json
import re
import shutil
from datetime import datetime

from common import bm25_top, stratified

ARMS = ("filum-facts", "filum", "bm25")
BM25_K = 5
CHUNK_CHARS = 16_000
INSTRUCTION = (
    "These are past conversations between the person and their assistant, in date order. Save every fact about the "
    "person and their life that the person states (people, places, things they own or use, plans, preferences, events, "
    "numbers and dates) with fact_record, one call per fact: the subject, the attribute and the value, with the date of "
    "the conversation as validFrom. When a fact changes, record the new value for the same subject and attribute. "
    "Do not answer anything else.")
# The turns an ingestion session may take: a chunk holds dozens of facts, each a call; a turn may write many calls.
INGEST_TURNS = 60
INGEST_OUTPUT = 8_000


def stratum(item):
    return "abstention" if item["question_id"].endswith("_abs") else item["question_type"]


def select(items, count, seed, used=()):
    strata = {}
    for item in items:
        strata.setdefault(stratum(item), []).append(item["question_id"])
    return stratified(strata, count, seed, used)


def when(date):
    plain = re.sub(r" \(\w+\) ", " ", date)
    try:
        return datetime.strptime(plain, "%Y/%m/%d %H:%M")
    except ValueError:
        return datetime.min


def sessions(item):
    """The history's sessions in date order (the file does not keep them in that order)."""
    rows = list(zip(item["haystack_dates"], item["haystack_sessions"]))
    return [r for _, r in sorted(enumerate(rows), key=lambda x: (when(x[1][0]), x[0]))]


def transcript(turns):
    return "\n".join(f"{'Assistant' if t['role'] == 'assistant' else 'Person'}: {t['content']}" for t in turns)


def question(item):
    return f"Today is {item['question_date']}. {item['question']}"


def question_after(chosen, item):
    history = "\n\n".join(f"[Conversation on {d}]\n{transcript(t)}" for d, t in chosen)
    return f"Here are past conversations with the person:\n\n{history}\n\n{question(item)}"


def chunks(item):
    """The history in date order, cut between conversations into about CHUNK_CHARS (a longer conversation alone)."""
    parts, current = [], ""
    for date, turns in sessions(item):
        block = f"[Conversation on {date}]\n{transcript(turns)}"
        if current and len(current) + 2 + len(block) > CHUNK_CHARS:
            parts.append(current)
            current = block
        else:
            current = f"{current}\n\n{block}" if current else block
    if current:
        parts.append(current)
    return parts


JUDGE = {
    "contains": "I will give you a question, a correct answer, and a response from a model. Please answer yes if the response contains the correct answer. Otherwise, answer no. If the response is equivalent to the correct answer or contains all the intermediate steps to get the correct answer, you should also answer yes. If the response only contains a subset of the information required by the answer, answer no. \n\nQuestion: {0}\n\nCorrect Answer: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.",
    "temporal": "I will give you a question, a correct answer, and a response from a model. Please answer yes if the response contains the correct answer. Otherwise, answer no. If the response is equivalent to the correct answer or contains all the intermediate steps to get the correct answer, you should also answer yes. If the response only contains a subset of the information required by the answer, answer no. In addition, do not penalize off-by-one errors for the number of days. If the question asks for the number of days/weeks/months, etc., and the model makes off-by-one errors (e.g., predicting 19 days when the answer is 18), the model's response is still correct. \n\nQuestion: {0}\n\nCorrect Answer: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.",
    "update": "I will give you a question, a correct answer, and a response from a model. Please answer yes if the response contains the correct answer. Otherwise, answer no. If the response contains some previous information along with an updated answer, the response should be considered as correct as long as the updated answer is the required answer.\n\nQuestion: {0}\n\nCorrect Answer: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.",
    "preference": "I will give you a question, a rubric for desired personalized response, and a response from a model. Please answer yes if the response satisfies the desired response. Otherwise, answer no. The model does not need to reflect all the points in the rubric. The response is correct as long as it recalls and utilizes the user's personal information correctly.\n\nQuestion: {0}\n\nRubric: {1}\n\nModel Response: {2}\n\nIs the model response correct? Answer yes or no only.",
    "abstention": "I will give you an unanswerable question, an explanation, and a response from a model. Please answer yes if the model correctly identifies the question as unanswerable. The model could say that the information is incomplete, or some other information is given but the asked information is not.\n\nQuestion: {0}\n\nExplanation: {1}\n\nModel Response: {2}\n\nDoes the model correctly identify the question as unanswerable? Answer yes or no only.",
}


def judge_prompt(item, response):
    kind = {"temporal-reasoning": "temporal", "knowledge-update": "update", "single-session-preference": "preference"}.get(item["question_type"], "contains")
    if item["question_id"].endswith("_abs"):
        kind = "abstention"
    return JUDGE[kind].format(item["question"], item["answer"], response)


def prompt(arm, item):
    if arm.startswith("filum"):
        return question(item)
    every = sessions(item)
    return question_after([every[i] for i in bm25_top([transcript(t) for _, t in every], item["question"], BM25_K)], item)


def ingest(backend, filum_mcp, work, item, env):
    """Builds <work>/<id>/home-facts: the events of preload, plus the facts an agent saves, chunk by chunk.

    Returns the question's measures; with "retry" when the run must stop (a limit, or the budget), so what it spent is
    journaled and the question is built again on a resume."""
    base, home = work / item["question_id"] / "home", work / item["question_id"] / "home-facts"
    if home.exists():
        shutil.rmtree(home)
    shutil.copytree(base, home)
    entry = {"question_id": item["question_id"], "chunks": 0, "saved": 0, "refused": 0, "cost_usd": 0.0, "seconds": 0.0, "errors": 0}
    for text in chunks(item):
        answer = backend.ask(f"{text}\n\n{INSTRUCTION}", filum=(filum_mcp, home, env), max_turns=INGEST_TURNS, max_output=INGEST_OUTPUT)
        entry["cost_usd"] = round(entry["cost_usd"] + answer.cost_usd, 6)
        if answer.limited or answer.stopped:
            return {**entry, "retry": True}
        records = [r for name, r in answer.results if name == "fact_record"]
        entry["chunks"] += 1
        entry["saved"] += sum(1 for r in records if "Refused" not in r)
        entry["refused"] += sum(1 for r in records if "Refused" in r)
        entry["seconds"] = round(entry["seconds"] + answer.seconds, 1)
        entry["errors"] += 1 if answer.error else 0
    return entry


def history_tokens(item):
    return sum(len(t["content"]) for s in item["haystack_sessions"] for t in s) / 4


def calls(item, arms):
    """The (input, output) tokens a question will likely take, for the estimate: the history read about three times
    while the facts are saved and the answers, at the answer model's price; a judgment per arm, at the judge's."""
    history = history_tokens(item)
    answers = [(3 * history, 20_000)] if "filum-facts" in arms else []
    for arm in arms:
        answers.append((30_000 if arm.startswith("filum") else history * BM25_K / max(1, len(item["haystack_sessions"])) + 2_000, 500))
    return answers, [(1_000, 5)] * len(arms)


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))
