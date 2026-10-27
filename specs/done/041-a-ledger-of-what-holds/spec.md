# A ledger of what holds

> Status: Implemented (2026-10-18). Reviewed (the owner, 2026-10-18, idea 1 of the state-of-the-art design, product version)
> Area: engine (facts: `FactService`, `FactTable`, fact tools, instructions)
> Author/date: federico rogora (drafted by Claude), 2026-10-18

## Problem

The facts table (`/facts.csv`, spec 037) keeps every value a fact had, with its days. The audit and the design study
(a design study of the state of the art) found what it cannot say:
- **A correction looks like a change.** "Sam lives in Lyon, not Turin, I got it wrong" closes Turin on a day as if Sam
  had moved; the memory then claims Turin was true until that day.
- **A fact cannot end without a new value.** "I sold the car" has no next value to record.
- **An earlier value is refused.** A value whose day is before the current one's is refused, so a history told out of
  order cannot be completed.
- **Saying it again is an error.** Recording the value a fact already has is refused, and a model reads it as a failure.
- **Who wrote it, when, and from which words are lost.** A row does not say when it was recorded, whether the person or
  the agent recorded it, or the words it came from.
- **Names.** "Sam" and "Sam Lee" are two subjects.
- **The past cannot be asked.** There is no way to ask what held on a given day.

## Goal

The facts are a ledger: each value says when it held, when it was recorded, by whom and from which words, and how it
ended (changed, corrected, retracted, ended); a past state can be asked; and nothing about it is tied to a domain.

## Non-goals

- No graph store and no new dependency: `/facts.csv` stays the one place the facts live, a file the person can open. An
  index (SQLite, approved) is left for when its size needs one.
- A person's hand edit of `/facts.csv` stays an edit of the file (spec 012); it is not read back as typed corrections.
- No automatic contradiction finding: that is consolidation's job (a later spec).

## Desired behavior

1. **Four columns more**, after the seven of spec 037, so the file stays readable by earlier versions (which ignore
   them): `recorded_at` (UTC, `yyyy-MM-ddTHH:mm:ssZ`), `ended` (empty while open; `changed`, `corrected`, `retracted` or
   `ended`), `author` (`agent` or `person`, empty when not known) and `quote` (up to 200 characters of the words of the
   first source event). A table with the seven columns only is read as before and gains the four when it is next
   written; its old rows have them empty.
2. **`fact_record` gains `correction`** (true when the person says the current value was wrong). The open value is then
   ended as `corrected`: its `valid_to` becomes its own `valid_from`, so it never held; the new value takes its time.
   With `validFrom`, the value that held on that day is the one corrected, open or not (amended after the independent
   audit). Without `correction`, the open value ends as `changed` on the new value's day, as before.
3. **`fact_end`, a new tool:** ends the open value of a subject and attribute without a new one, on a day (`on`, today
   when empty) as `ended`; with `retract`, as `retracted`, meaning it never held (`valid_to` = `valid_from`).
4. **An earlier value is placed in time.** A value whose day is before the open one's is inserted where it belongs: it
   holds from its day until the next recorded value starts, and the value that held on that day ends there as
   `changed`. A value recorded for a day that already has a value replaces it there, and the later said wins, however
   many values a day gets; the replaced one ends that day as `changed`. A value placed before one that `fact_end`
   ended closes where that value starts; a value placed inside a time that ended ends when that time ended; when the
   next value is the same one, it starts earlier instead of being added twice. Nothing is refused for its day (amended 2026-10-18, twice: a value can change
   several times in a day, and facts given in order with no day of their own are all of today).
5. **Saying it again succeeds.** Recording the value a fact already has writes nothing and answers that it is already
   recorded; so does a value that already held on the day given.
6. **Aliases are facts.** A fact with the attribute `alias` (`Sam Lee · alias: Sam`) makes the value another name of the
   subject: asking for the facts of `Sam` gives those of `Sam Lee`, in `facts_current`, `facts_history`, and in the facts
   that searches show (`FactLinks`). A subject can have several aliases: a new one does not close the others.
7. **The past can be asked.** `facts_current` gains `on` (a day): the values that held that day, corrected and retracted
   ones never among them.
8. **Corrected and retracted values** never answer what holds now or held on a day; `facts_history` shows them with how
   they ended.
9. **Lines and structured output** show how a value ended and who recorded it (`- Sam · city: Turin (2026-01-01, corrected
   2026-03-01 by the person)`); the structured content (spec 040) adds `ended`, `recordedAt`, `author` and `quote`.
10. **The fact policy** (`InstructionParts.FactPolicy`, spec 040) adds: a value the person says was wrong is recorded
    with `correction`; something that no longer holds, with nothing in its place, is ended with `fact_end`.
11. **Catalog.** `fact_end` joins the core profile (14 tools). The catalog's size budget of spec 031 (11,500 characters
    for every tool at once) becomes a budget per profile: the core profile at most 7,000 characters, every tool at most
    12,500. A host that offers every tool is one that chose to.

## Platform check

1. Generic: subjects, attributes and values of any domain; `alias` is the one reserved attribute.
2. Sensitivity: unchanged; a quote is at most what the source event already holds, and a private event gives no quote:
   its words never reach `/facts.csv`.

## Acceptance criteria

1. Given a seven-column `/facts.csv`, when a fact is recorded, then the file has eleven columns, the old rows keep their
   values with the new columns empty, and an engine reading only the seven columns still reads every row.
2. Given an open value, when the same value is recorded again, then the call succeeds, nothing is written and no
   revision is made.
3. Given Sam's city Turin from 2026-01-01, when Lyon is recorded with `correction`, then Turin is `corrected` with
   `valid_to` 2026-01-01, Lyon holds from 2026-01-01, `facts_current` says Lyon and `facts_current on 2026-02-01` says Lyon.
4. Given the same without `correction` and `validFrom` 2026-03-01, then Turin is `changed` and ends 2026-03-01, and on
   2026-02-01 Turin held.
5. Given an open value, when `fact_end` is called for its subject and attribute on a day, then it is `ended` that day and
   nothing holds now; with `retract`, it is `retracted` and never held.
6. Given Turin from 2026-03-01, when Paris is recorded from 2026-01-01, then Paris holds 2026-01-01 to 2026-03-01 and
   Turin still holds now; a second value from 2026-01-01 takes Paris's place there, and Paris ends that day.
7. Given `Sam Lee · alias: Sam` and `Sam Lee · city: Lyon`, when the facts of `Sam` are asked, then Lyon is among them.
8. Given a fact recorded by the agent from an event, then its row has `author` `agent`, a `recorded_at`, and a `quote`
   with the start of the event's words.
9. The core profile's catalog is at most 7,000 characters and the whole catalog at most 12,500 (tests).
10. Both instruction surfaces carry the new policy text (the test of spec 040).
