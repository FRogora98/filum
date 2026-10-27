# Values that change — tasks

> A small change: the plan is in the spec's Desired behavior.

- [x] **T1 — Changed lines in a read**
    - Do: `LineChanges.Of` (pairs each added line with the most similar removed one, between versions); `MemoryService.LineChangesAsync` (the last 20 revisions); `memory_read` notes, at most 30.
    - Verify: `ValuesThatChangeTests` criteria 1 to 3.

- [x] **T2 — What was said in a search**
    - Do: `memory_search` adds up to 5 matching messages, newest first; not in a consolidation pass; private messages only with `includePrivate`.
    - Verify: `ValuesThatChangeTests` criterion 4; the catalog snapshot and its size bound (criterion 5).

- [x] **T-close**
    - Do: LongMemEval `oracle` with Claude Code (Haiku 4.5), `--hook --consolidate`.
    - Knowledge updates on 2026-10-06: 0 of 4 before this spec, 2 of 4 with it (the two still wrong: a count the model took from its own collection instead of what was said, and a question it answered without searching). The whole 20 again when the owner decides.
