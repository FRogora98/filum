# A benchmark anyone can run

> Status: Implemented (2026-10-18). Reviewed (the owner, 2026-10-18, gap 1 of the engine audit, step C5 of the plan)
> Area: evaluation (`evals/bench/`, `src/Filum.Evals`)
> Author/date: federico rogora (drafted by Claude), 2026-10-18

## Problem

Filum's numbers on MemoryAgentBench's FactConsolidation and on LongMemEval come from a Python harness that was never in
this repository, and that harness drives an agent through a personal subscription:
- **An outsider cannot run it.** The scripts are not here.
- **The costs are not dollars.** A subscription's figures are "equivalent dollars", not what an API key would pay.
- **The C# runner does not reproduce them.** `src/Filum.Evals` is a second harness that produced none of the headline
  numbers.

Mem0, Supermemory, LongMemEval and MemoryAgentBench all publish harnesses an outsider can run.

## Goal

One command, with the user's own API key, builds Filum's memory from a benchmark's history and answers its questions.
The arms are Filum and the references it was compared with. The command writes:
- a result per question;
- a report with the paired test;
- the real dollars spent, within a cap given in advance.

## Non-goals

- No new benchmark: FactConsolidation (MemoryAgentBench) and LongMemEval, as they were run.
- No Mem0 or Graphiti arm: they need their own stacks and keys (a later spec may add them).
- Never in CI and never in `dotnet test`: the harness's own logic is unit-tested, its runs are by hand.
- No change to the engine or to `filum-mcp`.

## Current behavior

- `src/Filum.Evals`: scenario runs and LongMemEval on a running host, or on Claude Code.
- The scripts behind the published numbers are elsewhere.
- The step that writes a LongMemEval history into a memory folder ("preload") is a separate program, also elsewhere.

## Desired behavior

1. **`evals/bench/`** is a small Python harness (MIT, Python 3.11 or later; `requirements.txt` with `openai`, `pandas`,
   `pyarrow`). It is one command, `python evals/bench/bench.py <benchmark> [options]`, which does every step and
   resumes where it stopped:
   1. **Downloads the data** from pinned URLs. Each file is used only when its SHA-256 matches.
   2. **Builds `filum-mcp`** from this repository with `dotnet publish`, or uses `--filum-mcp`.
   3. **Builds each memory:**
      - LongMemEval: every turn as an event (item 5), then an agent saves the facts.
      - FactConsolidation: an agent saves every fact.
   4. **Answers with every arm**, each with the same model.
   5. **Judges.** LongMemEval: the authors' prompts. FactConsolidation: the authors' substring metric.
   6. **Writes the results.**
2. **Backends.**
   - `--backend openai` (the default) is any OpenAI-compatible endpoint (OpenAI, OpenRouter, a local server), with
     `OPENAI_API_KEY` and an optional `OPENAI_BASE_URL`.
     - Filum's arms reach `filum-mcp` over stdio as a minimal MCP client: its instructions as the system prompt, its tools
       as functions.
   - `--backend claude-code` is `claude -p`, as the published runs were made.
   - The key is read from the environment only. It is never printed, written or logged.
3. **Arms.**
   - **LongMemEval:**
     - `filum-facts`: the turns as events plus the facts an agent saved;
     - `filum`: the turns as events only;
     - `bm25`: the five sessions BM25 ranks first, in the prompt.
   - **FactConsolidation:**
     - `filum-facts`: the facts an agent saved one by one, with the explicit instruction of the published runs;
     - `bm25`: the ten chunks BM25 ranks first;
     - `long-context`: the whole history in the prompt.
4. **Money.**
   - Every call's cost comes from its usage and the model's prices: a small table for known models, or `--price-in` and
     `--price-out` in USD per million tokens. A model with no known price is refused.
   - Before it starts, the run estimates its cost and refuses to go above `--cap` (USD) unless `--force` is given.
   - During the run, it books each call's worst case before sending it and stops before the cap could be passed.
   - On `claude-code` the figures are the equivalent dollars Claude Code reports, and are labeled so.
5. **Preload in C#.** `dotnet run --project src/Filum.Evals -- preload` writes LongMemEval histories into memory folders:
   - each history session is a conversation;
   - each turn is an event dated by its session: the person's turns `said`, the assistant's `answered`;
   - the search vectors are made with the local model when it is there.
   This is the program the published LongMemEval rounds used.
6. **Results.** `<out>/results.jsonl` has one line per arm and question:
   - the question, the gold answer and the answer;
   - correct or not;
   - the tool calls;
   - tokens, cost, seconds, and the models (answer and judge).

   `<out>/report.md` gives:
   - each arm's accuracy, by ability (LongMemEval) or by hop (FactConsolidation);
   - the exact McNemar test of each pair of arms on the same questions;
   - the cost.
7. **Questions.**
   - `--ids <file>`: a list of question ids or indices.
   - `--count <n> --seed <s>`: a selection stratified by type in proportion to the whole set, ranked by a hash of the
     seed, as the published rounds drew theirs.
   - The selection is written to `<out>`, so a run says what it asked.

## Platform check

1. Generic: the benchmarks are public and domain-free, and the harness names no person.
2. Nothing personal is published:
   - the data is the benchmarks' own;
   - the memory folders and results stay where `--work` and `--out` point;
   - no key is written anywhere.

## Acceptance criteria

1. Given a downloaded file whose SHA-256 does not match, then the run stops and says which file.
2. Given `--cap 1` and an estimate above it, then the run refuses to start and prints the estimate; with `--force` it
   starts, and it still stops before the money spent passes the cap.
3. Given a model with no known price and no `--price-in`/`--price-out`, then the run refuses to start.
4. Given a stopped run, when it is started again with the same `--out`, then the questions already answered are not
   asked again and their cost counts against the cap.
5. The BM25 ranking, the chunking, the LongMemEval judge prompts, the substring metric and the stratified selection
   give the same results as the published runs on fixed inputs (unit tests with the published runs' values).
6. The exact McNemar test gives the published p-values on their counts: 23 to 8 gives p 0.011, and 6 to 3 gives p 0.51.
7. The MCP bridge lists `filum-mcp`'s tools and calls one, against a build of this repository (an integration test that
   needs no key).
8. `preload` writes a history's sessions as conversations of `said` and `answered` events, each dated by its session and
   in the file's order, as the published rounds wrote them (a test on a two-session synthetic item).
9. The README says how to run each benchmark in one command, what it costs, and what is a deviation from each
   benchmark's own protocol (the judge model, the explicit instruction).

## Open questions

None: the owner set the requirements (the user's own key, a result per question, one command), and the
technical choices are the engine's.
