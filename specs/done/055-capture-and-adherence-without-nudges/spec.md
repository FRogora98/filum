# Capture and adherence without the host's nudges

> Status: Implemented (2026-10-20)
> Area: engine, MCP
> Author/date: federico rogora (drafted by Claude), 2026-10-19

## Problem

The isolated harness of 2026-10-19 showed what Filum does when the host does not nudge the model toward memory. Claude
Code's own auto memory, for example, nudges it; a real user's host may not. The measurements are the knows-you rounds
on three people (spec 050) and one `mcp` scenario. Five gaps remain:

- **Capture.**
  - A promise or a to-do said once was never written: "find a moving company by end of May", "register at the
    municipality", "the concert went great".
  - A list the person asked for was written in the answer but not saved (`claim-trap-it`).
- **Rules reach the model and are ignored.** "Short bullet points", "short answers" and "metric only" were in the
  brief and in every recall, and were not followed in 1 to 2 of 3 rule probes per person, with Haiku 4.5.
- **Now and later are mixed.**
  - `facts_current` without `on`, the brief and the recall show every open value as holding now, including one that
    starts in the future.
  - On person 2, a move recorded for July left May's city with no value that holds now. Asked "which city do I live in
    right now?", the model said it did not know.
- **Abstention.** Asked for a sister never mentioned, the model named the daughter as the sister.
- **Closures.** An open item is sometimes ended on a later event that does not close it, for example a renewed
  passport taken as a different one.

The cost of the path without hooks is a gap of its own: about 1.35 times the input tokens of the hooks path.

## Goal

A fresh session answers from what was said, follows the rules the person set, and says "I do not know" for what
was never said. It does this with only Filum's own instructions, brief and recall, on a person never seen in
development.

## Non-goals

- No new tool, and no change to the facts file's format.
- No domain words in code, prompts or tests.
- No tuning to the frozen people 1-3: the verdict is on a new held-out person.
- The reranker, the embedder and persistent fact vectors are other Track B specs.

## Current behavior

- `FactService.CurrentAsync` (`src/Filum.Engine/Facts/FactService.cs`) without `on` keeps every open, held fact,
  whatever its `validFrom`.
- The brief's "Facts that hold now" and the recall use it (`src/Filum.Engine/Briefing/BriefService.cs`).
- Rules open the brief and are repeated in every recall, before what matched (spec 045, amended 2026-10-19).
- Consolidation in filum-mcp hands the hosted pass's policies and a last per-event check (spec 046, amended
  2026-10-19). It runs only when the model calls it, or when the Stop hook asks.

## Desired behavior

1. **Now is today.**
   - Without `on`, "current" means held today, on the host's clock. A replay (a benchmark) sets today explicitly, by
     a parameter or an environment variable; today is never inferred from the last event, since a person coming back
     after three months would get stale values. Every benchmark sets it to its probe day (spec 050, `evals/bench`).
   - A value that starts later is shown apart, as planned: "from <day>: <value>". It appears in the brief, in
     `facts_current`'s answer and in recall.
   - The value it will replace still shows as holding now.
2. **Rules, right before the answer.**
   - The recall also ends with one imperative line that names the rules that hold: "Before you answer, follow: …".
   - The instructions say a rule of the person's outranks the host's defaults of style and format (length, layout,
     units, language) for that answer. It never outranks safety or the host's own system rules: a memory that told a
     model to override its system prompt would be a way to inject instructions.
3. **Abstention.** The instructions say that what the memory does not hold is unknown. Never fill a person's
   relation, name or value in from something else; say it was never said. Abstention must not cost true answers: a
   false "I do not know" on something that was said is counted, and must not rise.
4. **Capture without nudges.**
   - The instructions, and the tidy pass's last check, name what must be saved even when the person does not say
     "remember": something promised or planned with a date; something done that closes an open item; a list or set
     the person asked to make or keep.
   - Without hooks, the first `memory_overview` of a session asks to save what the last session left unsaved. The
     first answer's added time stays within about 2 seconds at the 95th percentile, and it is reported.
5. **Closures.** An open item is ended only by an event that says it is done or dropped, in its own words. The
   ending keeps the closing event's id as its source, as a recorded value does, so it can be audited.
6. **Cost without hooks.** `memory_overview` in a fresh session gives the whole brief (at most about 2,000
   characters), the same text the hook gives, before the map. A model that calls it once has what the hooks give, at
   about the same tokens. A partial brief is measured only as an ablation, if the cost shows.

## Platform check

1. Generic: every change is about time, rules, unknowns and what lasts, for any person and any domain.
2. Nothing private is shown. A planned value follows the facts file's sensitivity like a current one. No personal
   data enters the repository: the tests use synthetic people.

## Acceptance criteria

1. Given a fact that starts tomorrow and the value it replaces, when the brief, `facts_current` and recall run
   today, then the replaced value shows as holding now and the new one as planned from its day. Unit test.
2. Given a rule fact, when recall runs on any message, then its text ends with the "Before you answer, follow"
   line; and the instructions limit a rule's reach to style and format. Unit tests.
3. Given an open item ended by the tidy pass, then the ending holds the closing event's id; and the tidy rules
   require that event's words. Unit test; the effect is measured in 5.
4. Given the instructions and the tidy rules, then they name promises, closures and asked-for lists as things to
   save without being asked. Unit test.
5. Given a fourth synthetic person, written and frozen before any run on it, when knows-you runs with Haiku 4.5
   (hooks and no hooks) and gpt-4o-mini (hooks) on the isolated harness, then:
   - Haiku with hooks is at least 20 of 23, and without hooks at least 17;
   - rule probes with hooks pass at least 2 of 3;
   - the abstention probes pass;
   - people 1-3 are run again and reported beside it, with no loss beyond 1 question per arm, and no more false
     "I do not know" answers on things that were said;
   - without hooks, the first answer's added time is reported against the 2-second cap;
   - gpt-4o-mini with hooks is reported against its range before (16 to 19), with no threshold.

   Person 4 holds, written before any run: at least 2 abstention probes, at least 1 probe of a planned change against
   the current value, at least 1 closure trap (an event that looks like done and is not), and at least 1 list the
   person asked for.
6. Given the `mcp` scenario suite on the isolated harness, when it runs before and after, then the scenarios that
   check saving, `claim-trap-it` among them, do not get worse. Both runs are reported.

## Open questions

None: both were answered in review (2026-10-19). "Today" is the host's clock, with an explicit override for replays
(item 1). A fresh session's first overview gives the whole brief (item 6).
