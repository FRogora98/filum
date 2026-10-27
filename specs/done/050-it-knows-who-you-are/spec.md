# It knows who you are: a measure

> Status: Implemented (2026-10-19; the baseline round follows). Reviewed (the owner, 2026-10-19, Track A's acceptance test; questions frozen before
> specs 046-049)
> Area: evaluation (`evals/bench`)
> Author/date: federico rogora (drafted by Claude), 2026-10-19

## Problem

LongMemEval and FactConsolidation measure answering questions over long histories. Neither measures what a personal
agent is for: that a fresh session, with no hints, knows who the person is, what holds now, what is still open, and
follows what they asked for sessions ago. Nothing measures what memory costs per turn either.

## Goal

One command measures, for a synthetic person told over eight sessions, how much a fresh session knows and follows,
with and without Filum's hooks, and what it costs in context and time.

## Desired behavior

1. **The data** is `evals/bench/data/knows-you.json`, frozen on 2026-10-19, before specs 046-049. It holds:
   - eight dated sessions of what the person says;
   - 20 questions with their expected answers, covering identity, current state, a past state, a correction, open
     items, preferences, abstention and a synthesis;
   - three rule checks, prompts whose answers must follow what the person asked earlier.
2. **`bench.py knows-you`** plays every session in its own session, then asks each question and each rule check in a
   fresh session. The arms:
   - `filum-hooks`: filum-mcp, plus the start brief and the recall on every message (Claude Code's hooks, or their
     same outputs on an API backend);
   - `filum`: filum-mcp alone (the instructions say to call `memory_overview` first);
   - `none`: no memory, the floor.
3. **The judge** asks, for each question, whether the answer gives the expected one. For each rule check, it asks
   whether the answer follows the rule.
4. **The report** gives, for each arm:
   - questions right of 20, by kind;
   - rules followed of 3;
   - the characters the hooks injected (the brief at start, the recall per turn: mean and maximum);
   - hook latency, p50 and p95;
   - cost.

   Arms are compared pairwise with the exact McNemar test (on so few questions, it shows only large differences).
5. **Backends,** as in spec 043:
   - Claude Code, with hooks written in the session's project settings;
   - an OpenAI-compatible API, where the brief is added to the system prompt and each recall to its message, the
     same text the hooks give.

## Acceptance criteria

1. The data file parses, has 8 sessions, 20 questions and 3 rule checks, and is committed before specs 046-049.
2. A run with a fake backend plays 8 sessions per arm and asks 23 probes per arm. Its report has every field of item
   4 (a unit test).
3. On the API backend, the `filum-hooks` arm's system prompt holds `filum-mcp brief`'s output, and each probe's message
   holds `filum-mcp log`'s recall (a test against a real build).
4. A first round on the current engine (045) is the baseline. Track A's round is measured against it, with its
   thresholds set before it runs.

## Baseline and Track A's thresholds (fixed 2026-10-19, before Track A's round)

- **Baseline** (engine with spec 045, gpt-4o-mini, judged by gpt-4o-2024-08-06): filum-hooks 17/23, filum 12/23,
  none 2/23.
- **Track A passes when:**
  - filum-hooks reaches at least 20/23 and loses no probe it answered right in the baseline;
  - filum (no hooks) reaches at least 17/23 and loses no probe it answered right in the baseline.
- Capture is reported apart from recall.
