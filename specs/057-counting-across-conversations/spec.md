# Counting across conversations

> Status: Draft
> Area: engine, MCP
> Author/date: federico rogora (drafted by Claude), 2026-10-27

## Problem

On LongMemEval_S, the questions that join several conversations are the largest group of errors. Over four rounds and
541 questions, the facts memory answered 82 of 127 multi-session questions (BM25 over the five best sessions: 76). Of
its 45 errors, about four in five are counts or totals of things said in different conversations: "how many pieces of
furniture did I buy, assemble, sell or fix", "how much did I spend on luxury items", "how many siblings do I have".
The model finds two or three of four items and counts those.

The cause is in the search, not in the model's arithmetic:
- `events_search` gives at most 8 matching messages (`MemoryOptions.EventsSearchMax`), then the best conversations
  within a budget. That fits a question with one answer, not a list whose items are said in eight conversations.
- One search uses one set of words. "Furniture" does not find "bookshelf" or "sofa"; a count needs the category and its
  members.
- Nothing tells the model that a count is a different job from a lookup: it adds up what it read, and stops early.

## Goal

A question that counts or adds up things said across conversations is answered from every item said, each with the day
and the conversation it was said in, counted once.

## Non-goals

- No change to how a memory is written, to the facts file or to the folder's format.
- No domain words in code, prompts or tests: "items", "a category and its members".
- No tuning on the questions the measure uses (see Acceptance 4).

## Current behavior

- `events_search(query, from, to, includePrivate)` ranks messages by words and, with the embedding model, by meaning,
  keeps the best 8 (`EventService`, `EventSearch.Fuse`), and shows the facts that may answer and the best
  conversations within `RecallBudgetChars` (`EventTools.SearchEvents`, `RecallPage`).
- The instructions say how to look something up; they say nothing about counting.

## Desired behavior

1. **A search that lists every mention.** `events_search` gains `every` (default false). With it, the search returns
   every matching message up to `MemoryOptions.EventsListMax` (new, default 100), ordered by day, one line each: its
   number, day, who said it and a short excerpt around the match (about 160 characters), and a closing line with how
   many were found and, when the cap was reached, that there may be more. No conversations are shown whole; the model
   reads one with `events_read` when a line is not enough. The same private rules as a normal search.
2. **Several words in one go.** The `query` of a listing search may hold several alternatives separated by `|`
   (a category and its members: "furniture | bookshelf | sofa | desk"); a message matches any of them, and is listed
   once.
3. **The instructions say how to count.** For "how many", "how much in total" or "which most": list with `every`, with
   the category and its likely members; keep each distinct item once (the same thing said twice is one), with its day;
   respect the time the question names; then count or add with `compute_run` when it is offered, and say the items in
   the answer.
4. **The engine API:** `IMemoryEvents.ListEventsAsync(userId, query, from, to, includePrivate, max)` gives the listing,
   so a host other than filum-mcp can use it.

## Platform check

1. Generic: listing every mention and counting distinct items works for any person and any domain.
2. Nothing private appears unless asked, exactly as in a normal search. A listing is bounded (`EventsListMax` lines of
   about 160 characters), so it cannot flood a context.

## Acceptance criteria

1. Given 30 messages that mention an item over 12 conversations, when `events_search` runs with `every`, then all 30
   are listed in day order with their numbers, once each, within the cap; with more than the cap, the closing line says
   so. Unit tests on the in-memory and folder stores.
2. Given a query "a | b", then a message that says only "b" is listed, and one that says both is listed once. Unit test.
3. Given the instructions, then they name `every` for counts and totals, distinct items, the time window and
   `compute_run`, and the full profile's instructions stay under 4,096 characters. Unit test.
4. **The measure** (LongMemEval_S, the facts memories already built, Haiku 4.5 through the isolated harness, judged as
   before):
   - development only on the 200 questions of the first round (seed `5c-20261017`);
   - the spec and the code are frozen, then the test set runs once: the other rounds' questions (about 290), with two
     arms on the same memories, this version and 1.0.0;
   - **passes** if, on the test set's multi-session questions, this version answers at least 8 more than 1.0.0 with
     exact McNemar p < 0.05, and all questions together lose nothing beyond 2 percentage points;
   - declared limits: the test set's errors were glimpsed (a list of them was read once before this spec), and its
     memories were built before the harness was isolated.

## Open questions

1. `EventsListMax` 100: enough for a person's year of mentions of one category, small enough for a context (about
   16,000 characters). Proposed as is.
2. Should `memory_search` (the files) also get `every`? Proposed: not now; the errors are in what was said.
