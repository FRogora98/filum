# The fast gate: keep or discard, in a millisecond

> Status: Implemented (2026-10-04). Reviewed (the owner, 2026-10-04: "parti dallo step direttamente successivo", after spec 021; phase E delegated). First slice of the roadmap's 022: keep or discard. Sensitivity, the target file, contradiction and skills are later slices.
> Area: engine (consolidation) / `Filum.Embeddings` / evals
> Author/date: federico rogora (drafted by Claude), 2026-10-04

## Problem

A consolidation pass hands every event since the last one to a model. Most of what people say to an assistant is not about their life: a recipe, a script, a translation, a question about a tool. On LongMemEval `S`, about half the sessions of a history are generic chats of that kind. The model reads them all, at full price, to find nothing in them.

## Goal

A small classifier on the event's vector (spec 023) tells in about a millisecond whether an event says something about the person's life. A pass is handed only those. Over the events that hold an answer, it keeps 98% or more, and a pass that would read nothing makes no model call at all.

## Non-goals

- **The turn is not gated:** what the person says is always answered and always kept in the log.
- **Nothing is deleted:** a discarded event stays in the log, searchable. It is only not handed to the pass.
- **No large model and no GPU:** a logistic regression on the 384 numbers of the vector the search already makes.

## Desired behavior

1. **Training, at no cost.**
   - The runner's `longmemeval --gate --embeddings <folder>` takes the person's messages from LongMemEval `S`. Those of the personal sessions are "keep"; those of the generic sessions (ShareGPT, UltraChat) are "discard".
   - It trains on the questions of one part of the file and tests on the others.
   - It picks the threshold that keeps at least 98% of the test's messages that hold an answer.
   - It writes the weights and the threshold, and a report: accuracy, evidence kept, share discarded.
   - The weights ship with the model, in `Filum.Embeddings`.
2. **The gate in a pass.**
   - With a model and a gate, the events a pass would read are scored, and only those at or above the threshold are handed to the model.
   - All of them are closed by the pass, as read.
   - With nothing left, the pass closes without calling the model.
   - The same in `filum-mcp`'s `memory_consolidate`.
3. **Off without the model:** a host or `filum-mcp` without the search model consolidates as today.

## Platform check

1. **Generic:** yes. The classifier tells "about the person's life" from "a generic task", in any domain and language the model reads.
2. **Sensitivity:** unchanged. Private events are not handed to a pass in the first place (spec 027).

## Acceptance criteria

1. Given the test questions of `S`, when the gate scores the person's messages, then it keeps at least 98% of those that hold an answer, and its accuracy and the share it discards are in the report.
2. Given a pass with a gate, when the pending events are a generic chat and a message about the person, then only the second is handed to the model, and both are closed.
3. Given a pass with a gate and only generic events pending, then no model is called and the pass closes them.
4. Given no model, then the pass is as today.

## Open questions

None.

## Result (2026-10-04)

`evals/reports/gate-20261004-171303/`: 500 questions of `S`, the first three fifths to train and the rest to test; `multilingual-e5-small` vectors; logistic regression; no cost.

| Measure (test questions) | Value |
|---|---|
| Messages holding an answer, kept | **99.1%** of 422 |
| Personal messages kept | 97.5% |
| Generic messages discarded | **71.5%** |
| Accuracy | 84.5% |
| Threshold | 0.349 |
