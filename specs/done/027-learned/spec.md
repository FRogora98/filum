# What Filum learns about the person, apart from what it is told

> Status: Implemented (2026-10-04). Reviewed (the owner delegated the specs of phase E, 2026-10-01). The approach differs from the roadmap's line for 027 (contextual bandits on proposals): it follows capability 9 of `FEATURES.md`, and the choice is asked to the owner.
> Area: engine (memory, instructions, consolidation) / the turn / MCP
> Author/date: federico rogora (drafted by Claude), 2026-10-04

## Problem

The memory keeps what the person tells: rules in the core, facts, collections. What it notices without being told has no home, so it is either lost or written into the core as if the person had said it:
- the names they give to people and things;
- their habits;
- how they like to be answered.

Capability 9 asks for an agent that learns these implicitly, keeps them visible and editable, tells them apart from what it was told, never lets them override an instruction, and never learns from private content.

## Goal

What the agent infers about the person lives in one file of its own, read at every message after the core, kept by the turn and by consolidation, and owned by the person like any other file.

## Non-goals

- **No new model and no training:** the inference is the turn's model's and consolidation's, written as text.
- **No learned changes to the agent's own code or tools:** only what it knows about the person.

## Desired behavior

1. **`/learned.md`.**
   - A document the platform creates with every memory (and on first need in an older one). Its lines are inferences: one each, with the day it was noticed and, from consolidation, the events it came from.
   - It is in the memory like any file: visible, editable, with its history.
   - It is left out of the generated map, because its lines are already in the instructions.
2. **Read at every message.**
   - The instructions carry its lines, after the core, under "What you have learned about the person (inferred, never above the core)".
   - At most 3,000 characters; past that, the newest lines are carried and the rest are counted.
   - `memory_overview` gives them too.
3. **Written live and by consolidation.**
   - The instructions (hosted and MCP) tell the agent what goes there: words for people, places and things; habits and rhythms; how the person likes answers.
   - The agent adds a line with `memory_append` and removes one that no longer applies with `memory_edit`.
   - Consolidation does the same from the events, citing them. It may add to the file even when the person edited it last, but not rewrite it then.
4. **Told always wins, private never teaches.**
   - What the person said outright goes in the core.
   - The instructions say an inference never overrides it.
   - Consolidation is never handed private events, for this or anything else.
5. **"What have you learned about me?"** is answered from the file, telling what was told (the core) from what was inferred.

## Platform check

1. **Generic:** yes. Traits are whatever a person shows, in any domain.
2. **Sensitivity:** private events are never read by consolidation; the file is the person's to correct or empty.

## Acceptance criteria

1. Given a new memory, then `/learned.md` exists, by the platform, with no inference; it is not in the map.
2. Given a line in it, when the person writes, then the turn's instructions carry it after the core.
3. Given more lines than fit, then the newest are carried and the rest counted.
4. Given the person edited the file last, when a pass appends to it, then the line is added; when a pass rewrites it, then it is refused.
5. Given a private event, then no pass is handed it.
