# The benchmark: LongMemEval on Filum

> Status: Implemented
> Area: evals / paper
> Author/date: federico rogora (drafted by Claude), 2026-10-01
> Roadmap: 020, the first spec of phase E's "benchmark and paper" track

## Problem

The paper's claim is that file-native, revisioned memory plus a check on claims makes a small model as reliable as a large one at remembering, updating and not making things up, at a fraction of the cost. Today nothing can support that claim:
- **Our 17 scenarios are ours:** short, synthetic, written by the people they test. A reviewer will rightly ask for a benchmark the field already uses.
- **The growth test so far is one run**, with 2 scenarios and one model: an anecdote, not a result.
- **Nothing compares Filum with the obvious alternatives:** no memory at all, or retrieving past conversations naively.

## Goal

The runner plays a stratified subset of LongMemEval on any host of the engine:
- with Filum and with the two baselines;
- with and without the claim check, and with and without revisions;
- on at least five models from small to large, with repetitions;
- within a cap.

It writes reports whose numbers the paper uses as they are.

## Non-goals

- **No other benchmark yet:** LoCoMo comes after, in its own spec (its data is CC BY-NC).
- **No change to the engine, the turn or the tools.** The benchmark measures them as they are; changes come in later specs, measured against this one.
- **No benchmark data in this repository.** LongMemEval is MIT (© 2024 Di Wu), but its files are large, so a script downloads them from the authors' release. Only our code, the authors' judge prompts (with their notice), our subsets (question ids) and our reports are committed.
- **No paper text yet:** spec 028.
- **No CI run:** the benchmark calls paid models, so it runs by hand.

## Current behavior

- The runner (`src/Filum.Evals`, spec 018) plays scenario files against a host and an `mcp` growth test. It knows nothing of LongMemEval.
- LongMemEval:
  - 500 questions over five abilities: information extraction, multi-session reasoning, knowledge updates, temporal reasoning and abstention;
  - three variants: `S` (about 40 sessions, about 115k tokens of history per question), `M` (about 500 sessions) and `oracle` (only the sessions with the evidence);
  - each question has a date, and each session has its own date.

## Desired behavior

**The protocol, for one question:**
1. A new synthetic person, on the host under test.
2. **Ingestion:** each history session, in date order, is a new chat. A session longer than a message may be (8,000 characters) is sent in consecutive parts of the same chat, "part 1 of 2"… The message gives the session's date and its transcript, framed the same way for every system and model ("Here is a conversation you had with the person on <date>…"). The system does what it would do with it; for Filum, that means keeping what is worth remembering.
3. **The question:** asked in a new chat, with the question's date, after all the sessions.
4. **Scoring:** the answer is judged with LongMemEval's own per-ability judge prompts. Abstention questions pass when the system says it does not know.

**The systems:**
- **Filum**, as hosted: memory as files with revisions, the claim check and the second attempt.
- **Ablations**:
  - Filum without the claim check (`Reliability:CheckModel` empty);
  - Filum without revisions: `memory_history` and `memory_undo` left out with `ExcludedTools`, so there is no going back.
- **How a system is chosen:** a Filum system is a host configured for it, and the run names it (`--system filum`, `filum-no-check`, `filum-no-revisions`). The sample host takes the configuration from the environment, with no package, so the prompt is the engine's own.
- **Baselines**, played by the runner itself with the same models, outside any host, calling the model's provider directly from a models file (the same `Models` and `Providers` shape as a host's configuration, keys from `<PROVIDER>_API_KEY`):
  - *no memory*: the question alone;
  - *naive retrieval*: the history's sessions, the top k by BM25 against the question, put before it.

**Large models through Claude Code** (the owner's subscription, not API credit):
- *Claude with Filum*: the `mcp` arm of 018 on the LongMemEval protocol. Each session is a new `claude -p` session with only `filum-mcp` mounted, and the question comes in one more.
- *Claude with no memory*: the question alone.
- *Claude with the whole history in its prompt*: the long-context reference.
- These run mostly on `oracle`, with few `S` questions, under `--max-sessions`, spread over days so the subscription's limits are never hit. The report writes the model version Claude Code declares.
- Here Filum is the MCP inside another agent, not its own loop with the claim check: the paper says so.

**The runs:**
- **Subset:** a stratified subset of `S`, the same number of questions per ability, chosen once with a fixed seed. Its question ids are committed (`evals/longmemeval/subset-<n>.json`). `oracle` is played too, to separate "did it store it" from "could it answer".
- **Models:** at least five, from small to large, from the host's catalog.
- **Repetitions:** three per question and system. The report gives the mean and the spread.
- **Cost:**
  - estimated before the run, per system and model, from the history's size;
  - a cap stops the run, as today;
  - the report gives tokens, latency and dollars per question and per correct answer.

**The results:** written as today (`report.md`, `results.json`). The runs the paper uses are committed under `evals/reports/` with the exact command, the commit and the date, so every number can be regenerated.

## Platform check

1. **Generic:** yes. The benchmark is a reader of a public dataset and a protocol; it names no domain, and the engine is unchanged.
2. **Sensitivity and privacy:**
   - LongMemEval's people are synthetic;
   - every run uses new synthetic accounts;
   - no dataset content is committed, only question ids and our scores;
   - the judge sees the question, the reference answer and the system's answer.

## Acceptance criteria

1. Given the download script, when it runs, then LongMemEval's files are fetched to a local, ignored folder and checked against their published sizes or hashes; nothing of them is committed.
2. Given the dataset, when the subset is built with the fixed seed, then it has the same number of questions per ability, and building it again gives the same ids.
3. Given one question, when it is played on Filum, then each session is one turn of a new chat in date order, the question comes in a new chat with its date, and the answer is judged with the prompt of its ability.
4. Given the same question, when the two baselines are played, then they use the same model, the same question framing and the same judge; the naive retrieval gets the top k sessions by BM25.
5. Given the ablations, when they run, then "without the claim check" makes no check call, and "without revisions" offers no `memory_history` or `memory_undo`.
6. Given a run, when the report is written, then it gives per system × model × ability:
   - accuracy (mean and spread over the repetitions);
   - abstention correctness;
   - changes claimed but not made;
   - tokens, latency, and dollars per question and per correct answer.
7. Given an estimate above the cap, when the run starts, then it refuses unless forced; during the run it stops at the cap and reports what it completed.
8. Given the runner's tests, when they run (no model, no key), then the reader, the subset, the protocol, the baselines and the scoring are tested on a small synthetic file in LongMemEval's format.

## Open questions

None. Answered by the owner on 2026-10-01:
- **Budget:** the credit available is about $15 (OpenAI and OpenRouter). The first run is small, about $3, under a cap of $4, and the runner may make it once the free part is done ("se costa davvero solo 3 dollari"). The large paper run waits for more credit, decided later.
- **Judge:** `gpt-5.4-mini`, fixed, on OpenAI.
- **Models:** `gemma-4-31b`, `glm-5.3-flash`, `deepseek-v4-flash` through OpenRouter, in Filum's loop. The large ones through Claude Code, managed carefully ("gestito bene e senza consumarmi token infiniti"). `gpt-5.5` is out for now.

The proposals that led to these answers:

- **The budget of a full run.** Ingestion is about 40 turns per question on `S`. Rough figures, before measuring:
  - with a small model, about $0.03 per question;
  - with a large one, $1–2 per question;
  - for 100 questions × 4 systems × 5 models × 3 repetitions: tens of dollars with small models, hundreds with large ones.

  → proposed:
  - a first run of 50 questions (10 per ability), 1 repetition, on all five models, to measure real costs;
  - then the full subset on the small models;
  - and only `oracle` plus fewer questions on the large ones.
- **The judge.** LongMemEval's authors used GPT-4o. → proposed: keep our fixed `gpt-5.4-mini` for cost, and judge one run with both to report how far they agree.
- **The five models.** → proposed: `gemma-4-31b`, `glm-5.3-flash`, `deepseek-v4-flash` (small), `gpt-5.4-mini` (middle) and `gpt-5.5` (large).
