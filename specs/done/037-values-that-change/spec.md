# Values that change: the old one stays in sight

> Status: Implemented (2026-10-06). Reviewed (the owner, 2026-10-06, on the proposal "files remember what they said before, and one search across files and what was said": "vai procedi")
> Area: engine (tools `memory_read`, `memory_search`)
> Author/date: federico rogora (drafted by Claude), 2026-10-06

## Problem

With every message kept (spec 036), Haiku through `filum-mcp` answered 14 of 20 on LongMemEval `oracle`. On knowledge updates it answered 0 of 4, and Gemma on the hosted service 2 of 4. The memory holds the facts; the model answers from the wrong place:
- **Overwritten values:** the model edits a file in place ("Premier Silver" becomes "Premier Gold") instead of `fact_record`, so a file shows only the new value. Asked "what was it before?", the model reads the file and finds nothing. The old value sits in the file's revisions, where it never looks.
- **Stale or conflicting values:** the model reads one source and stops. It answered "30 dozen eggs" from a file when what was said later says 20. Search covers files only, so the later message never comes up beside the file.

Instructions asking the model to check the log were tried (spec 031) and are not enough for small models.

## Goal

The engine puts the old value and the latest word in front of the model, whatever the model chooses to do:
- a file read shows what its changed lines said before;
- a search shows the dated messages that match, beside the files.

## Non-goals

- **No new tools,** and no change to how files and facts are written.
- **No automatic facts from files:** what changed is shown, not reinterpreted.

## Desired behavior

1. **`memory_read`: the old value beside the new.**
   - A line of the file that replaced another in one of its last 20 revisions carries a note after it: when it changed, and the part of the old line that differs (`(changed 2023-05-30; before: "…Premier Silver…")`).
   - At most 30 notes per read, and at most 200 characters each.
   - Lines that never changed read as before.
2. **`memory_search`: files and what was said, together.**
   - After the files' matching lines, it lists up to 5 matching messages from the log of events, newest first, each with its date and who said it.
   - A pass of consolidation does not get this section, since it already reads the events.
   - Private messages appear only with `includePrivate`, as for files.

## Platform check

1. **Generic:** yes. Any line of any file, any message.
2. **Sensitivity:** search follows the same rule for messages as for files. A note shows only a file's own past content, to whoever can read the file.

## Acceptance criteria

1. Given a document whose line was edited from "Premier Silver" to "Premier Gold", when it is read, then the line shows "Premier Gold" with a note that it was "Premier Silver" before, and when.
2. Given a collection whose row was updated (20 instead of 30), when it is read, then the updated row carries the old value.
3. Given a file never changed after it was written, when it is read, then no note appears.
4. Given a file line and a later message that both match, when the memory is searched, then the file's line and the dated message both appear, the newest message first; private messages only with `includePrivate`.
5. The tool catalog stays within its size bound.

## Open questions

None.

## Result (2026-10-06)

The 4 knowledge-update questions of the same subset, Claude Code with Haiku 4.5 through `filum-mcp`, with the hook (spec 036) and consolidation: **0 of 4 before this spec, 2 of 4 with it.**

- Fixed: "previous status", where the read showed Silver beside Gold with its date; and "where did I first keep", found through the search.
- Still wrong:
  - a count, which the model took from its own collection instead of what was said;
  - a question it answered without searching at all.
- The whole 20 are repeated after the owner's refactoring.
- The tests cover criteria 1 to 5 (`ValuesThatChangeTests`, the catalog's snapshot and size bound), and the hosted service's integration tests were updated for the new reads and searches.
