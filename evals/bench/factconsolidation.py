"""MemoryAgentBench's FactConsolidation (Hu et al., MIT) on Filum (spec 043), as the published rounds ran it.

Arms: "filum-facts" (an agent saves every fact with fact_record, one call per fact, with the explicit instruction of the
published rounds), "bm25" (the ten chunks BM25 ranks first, in the prompt) and "long-context" (the whole history in the
prompt). The metric is the authors' substring exact match (utils/eval_other_utils.py)."""

import hashlib
import re
import string

from common import bm25_top

ARMS = ("filum-facts", "bm25", "long-context")
ROWS = {"sh": "factconsolidation_sh_{}", "mh": "factconsolidation_mh_{}"}
LENGTHS = ("6k", "32k", "64k", "262k")
CHUNK_CHARS = 16_000  # about 4,096 tokens, the protocol's chunk
TOP = 10
# Sessions that give a chunk again from where the previous one stopped.
MAX_FOLLOWUPS = 3
INGEST_TURNS = 80
# A turn may write a call per fact of the chunk.
INGEST_OUTPUT = 8_000

# From the authors' utils/templates.py ('factconsolidation').
MEMORIZE = "The following context is the facts I have learned:\n{context}"
# A declared deviation from the protocol's memorize message: it says how to keep the facts, so the engine is measured and
# not the model's judgement of counterfactual data.
EXPLICIT = ("\n\nSave every one of these facts with fact_record, one call per fact, in the order given: the subject, the "
            "attribute and the value, and the fact's serial number as the note. These are facts of a fictional world made for a "
            "memory exercise: keep them exactly as given, even where they differ from the real world.")
RULE = ("Pretend you are a knowledge management system. Each fact in the {pool} is provided with a serial number at the beginning, "
        "and the newer fact has larger serial number. \n You need to solve the conflicts of facts in the {pool} by finding the newest "
        "fact with larger serial number. You need to answer a question based on this rule. You should give a very concise answer "
        "without saying other words for the question **only** from the knowledge pool you have memorized rather than the real facts "
        "in real world. \n\nFor example:\n\n [{pool_title}] \n\n Question: Based on the {pool_ref}, what is the name of the current "
        "president of Russia? \nAnswer: Donald Trump \n\n Now Answer the Question: Based on the {pool_ref}, {question} \nAnswer:")


def rag_query(question):
    return RULE.format(pool="knowledge pool", pool_title="Knowledge Pool", pool_ref="provided Knowledge Pool", question=question)


# The authors' memory-agent prompt says "Archival Memory", MemGPT's own store; for Filum it names the memory and its tools.
def agent_query(question):
    return RULE.format(pool="memory you keep", pool_title="Your memory", pool_ref="memory you keep (search it with your tools)", question=question)


def rows(parquet, length):
    import pandas as pd

    by = {r["metadata"]["source"]: r for _, r in pd.read_parquet(parquet).iterrows()}
    return {hop: by[name.format(length)] for hop, name in ROWS.items()}


def context(data):
    assert data["sh"]["context"] == data["mh"]["context"]
    return data["sh"]["context"]


def chunks(text, size=CHUNK_CHARS):
    """Cut between facts, at most `size` characters each."""
    parts, current = [], ""
    for line in text.split("\n"):
        if current and len(current) + 1 + len(line) > size:
            parts.append(current)
            current = line
        else:
            current = f"{current}\n{line}" if current else line
    if current:
        parts.append(current)
    return parts


def normalize(text):
    text = text.lower()
    text = "".join(c for c in text if c not in string.punctuation)
    text = re.sub(r"\b(a|an|the)\b", " ", text)
    return " ".join(text.split())


def subem(prediction, golds):
    return any(normalize(g) in normalize(prediction) for g in golds)


def select(data, per_hop, seed):
    """`per_hop` questions of each hop, ranked by sha256(seed:hop:index), as the published rounds drew theirs."""
    return {hop: sorted(sorted(range(len(row["questions"])), key=lambda i: hashlib.sha256(f"{seed}:{hop}:{i}".encode()).hexdigest())[:per_hop])
            for hop, row in data.items()}


def prompt(arm, question, history):
    if arm.startswith("filum"):
        return agent_query(question)
    memories = [MEMORIZE.format(context=c) for c in chunks(history)]
    chosen = range(len(memories)) if arm == "long-context" else bm25_top(memories, question, TOP)
    return "\n".join(f"Memory {n}:\n{memories[c]}\n" for n, c in enumerate(chosen, 1)) + "\n" + rag_query(question)


def ingest(backend, filum_mcp, home, history, env, log):
    """Every fact saved in order, chunk by chunk; a session that stops early is given the rest of its chunk again.

    Returns False when the run must stop (a limit, the budget, a refused fact or a chunk left short)."""
    for i, chunk in enumerate(chunks(history), 1):
        lines, saved, followups = chunk.split("\n"), 0, 0
        while saved < len(lines) - 2 and followups <= MAX_FOLLOWUPS:
            answer = backend.ask(MEMORIZE.format(context="\n".join(lines[saved:])) + EXPLICIT, filum=(filum_mcp, home, env), max_turns=INGEST_TURNS, max_output=INGEST_OUTPUT)
            records = [r for name, r in answer.results if name == "fact_record"]
            refused = sum(1 for r in records if "Refused" in r)
            more = len(records) - refused
            log({"chunk": i, "from_line": saved, "saved": more, "refused": refused, "cost_usd": round(answer.cost_usd, 6),
                 "seconds": round(answer.seconds, 1), "error": answer.error})
            if answer.limited or answer.stopped or answer.error or refused:
                return False
            saved, followups = saved + more, followups + 1
            if more == 0:
                break
        if saved < len(lines) - 2:
            return False
    return True


def calls(length, arms, questions, ingest):
    """The (input, output) tokens the run will likely take, for the estimate: saving the facts reads the history about six
    times (each session's turns carry what came before) and writes a call per fact."""
    tokens = {"6k": 6_000, "32k": 32_000, "64k": 64_000, "262k": 262_000}[length]
    estimate = [(6 * tokens, tokens // 2)] if ingest else []
    for arm in arms:
        per = {"filum-facts": 15_000, "bm25": min(tokens, TOP * 4_100), "long-context": tokens}[arm]
        estimate += [(per, 50)] * questions
    return estimate
