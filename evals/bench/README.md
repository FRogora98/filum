# Filum on public memory benchmarks

One command runs Filum on a public memory benchmark with **your own API key** and writes a result per question
(spec 043). It does every step and resumes where it stopped:
- it downloads the data, pinned and checked by SHA-256;
- it builds `filum-mcp` from this repository;
- it builds the memories, asks every arm the same questions, judges the answers and writes a report with the paired
  test.

It is run by hand, never in CI. It spends real money, within a cap you set.

## Needs

- Python 3.11 or later: `pip install -r evals/bench/requirements.txt`.
- The .NET 10 SDK, to build `filum-mcp` and to write the LongMemEval memories.
- An OpenAI-compatible endpoint:
  - `OPENAI_API_KEY` in the environment;
  - `OPENAI_BASE_URL` for anything other than OpenAI: OpenRouter, or a local server.

  The key is read from the environment only. It is never printed, written or logged.

## Run

```sh
# LongMemEval_S: 20 questions stratified by type; arms filum-facts, filum, bm25; judged by gpt-4o-2024-08-06 (the official judge)
python evals/bench/bench.py longmemeval --model gpt-4o-mini --count 20 --cap 5

# MemoryAgentBench FactConsolidation at 64k: 25 single-hop and 25 multi-hop; arms filum-facts, bm25, long-context
python evals/bench/bench.py factconsolidation --length 64k --model gpt-4o-mini --per-hop 25 --cap 5

# It knows who you are (spec 050): a synthetic person over ten sessions, then 20 questions and 3 rule checks in fresh
# sessions; arms filum-hooks (the start brief and the recall on every message), filum, none. Three people:
# --data-file data/knows-you.json (default), data/knows-you-2.json, data/knows-you-3.json. Each session runs with
# FILUM_TODAY set to its day and each probe to the probe day, so "now" is the person's, not the machine's. With
# --backend claude-code, every claude -p runs isolated: `python evals/bench/bench.py isolation-check` shows it.
python evals/bench/bench.py knows-you --model gpt-4o-mini --cap 2

# The report again, from what a run wrote
python evals/bench/bench.py report --out evals/results/longmemeval
```

### Files

| Where | What |
|---|---|
| `evals/data/` | the data |
| `evals/work/<benchmark>/` | the memory folders, which you can open: they are Filum memories |
| `evals/results/<benchmark>/results.jsonl` | one line per arm and question: the answer, the gold, correct or not, the tool calls, tokens, cost, the models |
| `evals/results/<benchmark>/report.md` | the report |
| `evals/results/<benchmark>/ids.json` | the questions asked |

Git ignores all three folders.

### Options

| Option | What it does |
|---|---|
| `--ids <file>` | asks the questions in the file instead of a selection |
| `--seed` | changes the selection |
| `--arms` | chooses the arms |
| `--parallel` | how many calls run at once (default 2) |
| `--tools full` | gives `filum-mcp` every tool (default: as shipped, the core profile) |
| `--filum-mcp <exe>` | answers through another build |
| `--backend claude-code` | asks `claude -p` instead of an API, as the published runs did. Its figures are Claude Code's equivalent dollars, not money paid |

## Money

Every call is priced from its usage:
- the known models are in `money.py`;
- any other model needs `--price-in` and `--price-out` (USD per million tokens), and `--judge-price-in` and
  `--judge-price-out` for the judge;
- a model with no price does not start.

The run spends no more than you allow:
1. **Before it starts,** it checks the key and the prices (before any download), prints an estimate (judgments at the
   judge's price) and refuses to start above `--cap`. `--force` starts it anyway.
2. **While it runs,** it books each call's worst case before sending it: its prompt and its longest answer, which the
   request caps with `max_tokens`. It stops before the cap could be passed.
3. **On a restart,** a run started again with the same `--out` skips what was answered, and counts against the cap
   everything already spent, including a memory left half built.

An answer that failed, or a judgment that failed three times, is kept with its cost, reported as "not judged" (never
as wrong), and asked again on a restart. With `OPENAI_BASE_URL` set, LongMemEval needs `--judge-model`: the official
judge may not be on your endpoint.

Rough costs with gpt-4o-mini:

| Run | Ingestion | Answers and judgments |
|---|---|---|
| LongMemEval, a question with three arms | about $0.05-0.10 (about 125,000 tokens of history read three times) | about $0.03 |
| FactConsolidation at 64k, 50 questions | about $0.30-0.60 (about 1,800 facts, a call each, the chunk read again at every turn) | about $0.10-0.20 |

## What differs from each benchmark's own protocol

- **The facts are saved by an agent with an explicit instruction**, Filum's way of building its memory:
  - LongMemEval: "save every fact about the person ... with fact_record";
  - FactConsolidation: "save every one of these facts with fact_record, one call per fact".

  FactConsolidation's own memorize message only asks to keep the facts. Its facts are counterfactual, and a model's
  judgement of them would be measured instead of the memory.
- **The prompt names Filum's memory.** FactConsolidation's memory-agent prompt says "Archival Memory", MemGPT's name for
  its own store. For Filum it names the memory and its tools.
- **The judge:**
  - LongMemEval's judge is the authors' prompts on `gpt-4o-2024-08-06`, as the official protocol, unless
    `--judge-model` changes it. The published Filum rounds used `claude-sonnet-5-5` on Claude Code.
  - FactConsolidation is scored by the authors' substring metric, with no model.
- **The same model for everything.** One model answers every arm and saves the facts.

## How the published numbers were made

- **It knows who you are:** made with this harness (`bench.py knows-you`), on the engine commits the main README names.
- **FactConsolidation and LongMemEval_S:** made with the scripts this harness was written from, not with `bench.py`, on
  the engine commits the main README names. Since 2026-10-19 they run Claude Code with the same isolation as this
  harness (`backends.py`, `IsolatedConfig`): the model sees nothing of the account and no memory of Claude Code's own.
  - answers by `claude-haiku-4-5-20251001` (gpt-4o-mini for FactConsolidation at 262k);
  - facts saved by `claude-haiku-5-5`;
  - the LongMemEval judge `claude-sonnet-5-5`.

On the real data, this harness's question selections, BM25 prompts, chunks and judge prompts are identical to those
scripts' (checked when it was written). It answers once per question, with one model for everything, so it reproduces
the protocol, not the exact published configuration. `--backend claude-code` comes closest; `--backend openai` lets
anyone check the result with a model of their own. The per-question results of the published rounds are not in this
repository.
