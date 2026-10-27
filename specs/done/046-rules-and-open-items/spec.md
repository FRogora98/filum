# Rules and open items, caught even when a turn misses them

> Status: Implemented (2026-10-19). Reviewed (the owner, 2026-10-19, Track A; capture must not depend on in-turn tool
> calls alone, no new tools, the core profile at 14 tools or fewer)
> Area: engine (facts, brief, consolidation, instructions), filum-mcp (profile, instructions)
> Author/date: federico rogora (drafted by Claude), 2026-10-19

## Problem

Spec 050's baseline shows what a small model writes during its turns: only facts. It wrote no core, no rule ("keep
answers short, in bullet points") and no open item ("I promised Ana the photos"), not even the person's name. A fresh
session then breaks the rule and forgets the promise:
- open items 0 of 2;
- rules 1 of 3.

There is also a gap without hooks. `memory_overview`, the first call the instructions ask for, shows the core and the
map but not the facts, so the no-hook path scored 12 of 23.

## Goal

Lasting rules and open items are kept as typed facts, with no new tool. They come right after the core in every brief.
Consolidation catches them, and who the person is, when the turns missed them. And the first call of a session gives
the same brief with or without hooks.

## Desired behavior

1. **Two more reserved attributes,** besides `alias`:
   - `rule`: a lasting instruction, with its reason in the note;
   - `open`: something promised or still to do, with its due day in the note when there is one.

   Like `alias`, they hold many values at once: a new value never closes the others. The same value again changes
   nothing. A done or dropped item ends with `fact_end`.
2. **`fact_end` takes `value`,** so one of several values ends. Without it, every open value of the attribute ends, as
   before.
3. **The brief** (spec 045) runs in this order:
   1. core;
   2. **rules** (each with its reason);
   3. **open items** (each with its due day and since when);
   4. the other facts;
   5. learned;
   6. map;
   7. what was said.

   When messages are waiting to be tidied, it ends with how many, and says to call `memory_consolidate` after
   answering the first request.
4. **`memory_overview` returns the brief,** with the enabled skills and a package's prompt. So the no-hook path's first
   call gives the same rules, open items and facts.
5. **Consolidation is the guaranteed extractor.** Its instructions ask for:
   - who the person is, in the core;
   - lasting rules and open items, as facts, from what was said;
   - an open item closed when the events say it is done.

   It still runs on the host's model, under the rules in code.
6. **filum-mcp's core profile** offers `memory_consolidate` instead of `memory_delete`, so the safety net is there by
   default; it stays at 14 tools. `memory_delete` stays in the full profile.
7. **The fact policy** (`InstructionParts.FactPolicy`, shared by every surface) adds the two attributes. The instructions
   are trimmed so both profiles stay under 4,096 characters.

## Amended after the first Track A round (2026-10-19)

The round showed the model never calls `memory_consolidate` on its own (0 calls), so the safety net never ran.
- **`filum-mcp tidy`, a `Stop` hook.** When more than 10 messages wait, it asks once per session, after the answer is
  shown, to call `memory_consolidate`. It never asks twice in a turn (`stop_hook_active`).
- **Without hooks,** `memory_overview` shows the newest waiting messages, at most 1,500 characters, and asks to record
  what the memory lacks.
- **The core profile** keeps a way to forget: `memory_delete` is back, and `facts_history` moves to the full profile.
- **Spec 050** reports which path wrote each item, and the latency and tokens of a fresh session's answer.

## Amended after the second Track A round (2026-10-19)

The rules `memory_consolidate` hands to the model in filum-mcp were a shorter list than a hosted pass's: they asked for
neither who the person is, nor rules, nor open items, so consolidation could not catch a promise the turn missed.
- **They now carry the same policies** (`InstructionParts`): the core, facts, rules and open items, what lasts.
- **They end with a last check:** read the events once more, one by one, against the memory: who someone is, a value
  that holds now or changed, a rule, something promised or still to do, something done that closes an open item.
- The page stays at 16,000 characters: in the rounds seen, what waited was far shorter, so a smaller page would change
  nothing measured.

## Platform check

1. Generic: `rule` and `open` are domain-free, like `alias`.
2. Sensitivity: unchanged; private sources give no quote (spec 041).

## Acceptance criteria

1. Two `rule` facts of one subject both hold, and the same rule again changes nothing. `fact_end` with `value` ends one
   `open` item and leaves the other.
2. The brief lists rules (with reasons) and open items (with due days) right after the core. Neither appears again among
   the other facts.
3. With pending messages, the brief says how many and to call `memory_consolidate`. With none, it says nothing about
   it.
4. `memory_overview` returns the brief's sections, including the facts.
5. filum-mcp's core profile has 14 tools, with `memory_consolidate` and without `memory_delete`. Both instruction texts
   are under 4,096 characters, and the fact policy is in each of them and in the consolidation instructions.
6. Spec 050's rerun reports capture apart from recall: for each question, whether what it needs was written, whether
   it was in the brief or the recall, and whether it was answered.
