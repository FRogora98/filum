# Read a whole conversation

> Status: Implemented (2026-10-17). Reviewed (the owner, 2026-10-17, "hypothesis 2, read a conversation whole, in order, within a budget")
> Area: engine (tools `events_search`, new `events_read`)
> Author/date: federico rogora (drafted by Claude), 2026-10-17

## Problem

`events_search` shows at most `EventsSearchMax` (8) matching messages, each as an excerpt of about 1,000 characters
around the first word of the query, with the reply after it cut to 300 characters. When the answer sits elsewhere in
the same conversation (a detail a few turns away, a long reply of the assistant), the model never sees it. On a
benchmark of long chat histories, a baseline that hands the model five whole conversations answered more questions about
a single conversation than Filum did, and the gap was where the excerpt stopped.

## Goal

A model that found the right conversation can read all of it, in order, within a budget, and reach the rest of a longer one.

## Non-goals

- No change to how `events_search` ranks or how many matches it shows.
- No conversation-level ranking, and no change to facts.
- No change to what is kept.

## Desired behavior

1. **`events_search` names each match.** Every match shows its event number (`#123`) after its date, so it can be read
   in its conversation. Nothing else in its output changes.
2. **`events_read`, a new tool.**
   - **Argument:** `event`, the number of an event (from `events_search`).
   - **What it gives:** every message of that event's conversation (said, answered, imported), oldest first, in full: its
     number, date and time, who said it and the text, under the conversation's title when it has one.
   - **An event with no conversation** (an import, a message logged outside a chat) gives the messages of the same day.
   - **Budget:** at most 20,000 characters of messages per call; a message is never cut. A conversation within it comes
     whole; a longer one comes from up to half the budget before the event, and the result says how many messages come
     before and after, with an event number to read each part (the catalog's size budget of spec 031 left no room for a
     paging argument).
   - **Refusals and privacy:** an unknown or private event answers that there is no such event, and the private messages
     of a conversation are left out. Private messages are reached only through `events_search` with `includePrivate` (the
     catalog's size budget of spec 031 left no room for one more argument).
3. **Description:** "Read the whole conversation of an event number from events_search, in order." `events_search`'s own description loses "in every conversation" (the catalog's size budget of spec 031).

## Platform check

1. Generic: what was said, in any domain; no domain word.
2. Sensitivity: private messages never come out of `events_read`; one person's events only.

## Acceptance criteria

1. Given matches, when `events_search` runs, then each shows `#<id>` after its date and the rest of its line as before.
2. Given a conversation of three messages, when `events_read` is called with the second one's number, then all three
   come back in order, in full.
3. Given another conversation, when the first is read, then none of the other's messages appear.
4. Given an event with no conversation, when it is read, then the messages of that day come back.
5. Given a conversation longer than the budget, when it is read, then whole messages up to the budget come back with
   an event number for the rest, and reading that event gives it.
6. Given a private message, when the conversation is read, then it is left out; an unknown or private event number
   answers that there is no such event.
7. The tool catalog snapshot lists `events_read` with its description and arguments.
