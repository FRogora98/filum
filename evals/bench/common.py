"""The pieces every benchmark shares (spec 043): pinned data, selection, BM25, the paired test, the journal."""

import hashlib
import json
import math
import re
import urllib.request
from collections import Counter
from pathlib import Path

# Each file at a fixed revision, used only when its SHA-256 matches. Both datasets are MIT.
DATASETS = {
    "longmemeval_s": (
        "https://huggingface.co/datasets/xiaowu0162/longmemeval-cleaned/resolve/98d7416c24c778c2fee6e6f3006e7a073259d48f/longmemeval_s_cleaned.json",
        "d6f21ea9d60a0d56f34a05b609c79c88a451d2ae03597821ea3d5a9678c3a442",
    ),
    "factconsolidation": (
        "https://huggingface.co/datasets/ai-hyz/MemoryAgentBench/resolve/7ea066982b140a19337e17e60d45d4076e042faf/data/Conflict_Resolution-00000-of-00001.parquet",
        "24d5c3f09ce0ce15625cb9f8a98f44f0d864ca6c94d7b4ad04eb697ca3a5ff45",
    ),
}

# The same for every arm: a host's own prompt can make the model a coding assistant otherwise.
PERSONA = "You are the person's personal assistant. Answer their question using the tools and the information available to you."

WORD = re.compile(r"[^\W_]+", re.UNICODE)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def ensure_data(name, folder, opener=urllib.request.urlopen):
    """The dataset's file in `folder`, downloaded when missing; stops the run when its hash does not match."""
    url, expected = DATASETS[name]
    path = Path(folder) / url.rsplit("/", 1)[1]
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        partial = path.with_suffix(path.suffix + ".part")
        with opener(url) as response, open(partial, "wb") as f:
            while block := response.read(1 << 20):
                f.write(block)
        partial.replace(path)
    actual = sha256(path)
    if actual != expected:
        raise SystemExit(f"{path.name}: SHA-256 {actual} differs from the pinned {expected}; delete it to download it again.")
    return path


def words(text):
    return WORD.findall(text.lower())


def bm25_top(documents, query, k):
    """Okapi BM25, k1 1.2, b 0.75, the engine's: the indices of the k best documents, in their own order."""
    docs = [words(d) for d in documents]
    counts = [Counter(d) for d in docs]
    average = sum(len(d) for d in docs) / max(1, len(docs))
    terms = list(dict.fromkeys(words(query)))
    frequency = {t: sum(1 for c in counts if t in c) for t in terms}
    scores = []
    for i, (d, c) in enumerate(zip(docs, counts)):
        score = 0.0
        for t in terms:
            tf = c.get(t, 0)
            if tf:
                idf = math.log(1 + (len(docs) - frequency[t] + 0.5) / (frequency[t] + 0.5))
                score += idf * tf * 2.2 / (tf + 1.2 * (1 - 0.75 + 0.75 * len(d) / max(1, average)))
        scores.append((-score, i))
    return sorted(i for _, i in sorted(scores)[:k])


def rank(seed, key):
    return hashlib.sha256(f"{seed}:{key}".encode()).hexdigest()


def stratified(strata, count, seed, used=()):
    """`count` ids stratified in proportion to the whole set, each stratum ranked by a hash of the seed, none of `used`.

    strata: {name: [ids]} of the whole set. The published LongMemEval rounds drew theirs this way."""
    used = set(used)
    total = sum(len(ids) for ids in strata.values())
    quota = {name: round(count * len(ids) / total) for name, ids in strata.items()}
    # Rounding can miss the count by one or two: the largest strata absorb the difference.
    for name in sorted(strata, key=lambda n: -len(strata[n])):
        if sum(quota.values()) == count:
            break
        quota[name] += 1 if sum(quota.values()) < count else -1
    chosen = []
    for name in sorted(strata):
        pool = [i for i in strata[name] if i not in used]
        if len(pool) < quota[name]:
            raise SystemExit(f"{name}: {len(pool)} unused questions, {quota[name]} wanted")
        chosen.extend(sorted(pool, key=lambda i: rank(seed, i))[:quota[name]])
    return chosen


def mcnemar(only_first, only_second):
    """The exact two-sided McNemar p-value of a paired comparison: the questions only the first arm got right, and those
    only the second got right."""
    n = only_first + only_second
    if n == 0:
        return 1.0
    tail = sum(math.comb(n, i) for i in range(min(only_first, only_second) + 1)) / 2 ** n
    return min(1.0, 2 * tail)


class Journal:
    """One JSON line per answer, appended as it is judged; what is there is not asked again, unless its row says to
    retry (an error, a judge that failed, a memory left half built). Every row's cost counts."""

    def __init__(self, path, key):
        self.path, self.key = Path(path), key
        self.rows = [json.loads(line) for line in self.path.read_text(encoding="utf-8").splitlines() if line.strip()] if self.path.exists() else []

    def done(self):
        return {self.key(r) for r in self.rows if not r.get("retry")}

    def spent(self):
        return sum(r.get("cost_usd", 0) + (r.get("judge") or {}).get("cost_usd", 0) for r in self.rows)

    def add(self, row):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        with self.path.open("a", encoding="utf-8") as f:
            f.write(json.dumps(row, ensure_ascii=False) + "\n")
        self.rows.append(row)
