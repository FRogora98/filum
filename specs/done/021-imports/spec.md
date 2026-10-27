# Absorb: imports from files

> Status: Implemented (2026-10-04). Reviewed (the owner delegated the specs of phase E, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (the log of events) / MCP / HTTP
> Author/date: federico rogora (drafted by Claude), 2026-10-04
> Roadmap: phase E, spec 021 ("an export becomes memory with provenance; a preview shows what would be kept before anything is written")

## Problem

Filum learns only what the person types. A person already has years of their life in files: chat exports, mailboxes, calendars, spreadsheets. None of it can enter the memory today, except by pasting it into a chat one message at a time.

## Goal

A file the person already has becomes events in their log, each with its own date and source. Consolidation then turns them into memory, with provenance. A preview shows what would be imported before anything is written.

## Non-goals

- **No connectors and no OAuth:** files only. Live sources are spec 024.
- **No keep-or-discard model:** every item becomes an event; deciding what matters is consolidation's today and the fast gate's later (022).
- **No health exports, PDFs, audio or photos:** they need their own parsers, or models. Later.
- **No screen in the app:** the endpoint is there for the app; the screen is the app's own work.

## Current behavior

- The log of events (spec 030) has the kind `imported` and the source `import`, but nothing produces them.
- Consolidation already reads `imported` events like what was said.

## Desired behavior

### 1. The formats

| Format | What one item is | Its date | Its identity |
|---|---|---|---|
| `whatsapp` (`.txt` export, Android and iOS) | one message: who and what, with following lines joined | the message's | the chat's name, time, sender and text |
| `mbox` (mail) | one mail: from, subject, the plain text of its body | its `Date` | its `Message-ID`, or its content |
| `ics` (calendar) | one event: title, place, description | its start | its `UID` and start |
| `csv` | one row: every field with its name | its first column whose every value is a date, otherwise the import's | its content |

- The format is detected from the file name and content, or given.
- WhatsApp's date order (day or month first) is detected from the file.
- Lines that are not messages (system notices) are skipped and counted.
- A mail's body is the plain-text part, or the HTML part with its tags removed. Quoted-printable and base64 are decoded. A body is cut at 4,000 characters.

### 2. The import

- Every item becomes one `imported` event:
  - **source:** `import`;
  - **date:** the item's own, as `occurred_at`; the import's moment is `recorded_at`;
  - **text:** the item, with who said it when there is a who;
  - **sensitivity:** chosen for the whole import (normal by default).
- **One file, one source:** all its items share one id, the import's `conversationId`, which is how it is found and forgotten.
- **No duplicates:** an item whose identity is already in the log is skipped, and counted.
- **Preview:** the same call with `preview` true writes nothing. It returns the format, how many items are new, how many are duplicates, how many lines were skipped, and the first five new items as they would be recorded.
- **Limits:** at most 5,000 items and 20 MB per file. Above them nothing is written, and the person is told to split the file.
- **Forgetting an import** is forgetting its source: `POST …/memory/forget` with the import's id (spec 030).

### 3. Where it is offered

- **`filum-mcp`:** the tool `memory_import(path, format?, sensitivity?, preview = true)` reads a file on the person's machine.
  - The host's model previews first, shows the person what would be imported, and imports with `preview` false only when they agree.
  - Afterwards the host tidies the events with `memory_consolidate` (spec 030).
- **Hosted:** `POST …/memory/import`, a multipart upload (`file`, `format`, `sensitivity`, `preview`). It returns what the preview returns, plus the import's id. Consolidation picks the events up like any others, 200 per pass, until none are pending.

## Platform check

1. **Generic:** yes. Formats are file formats, not domains; the items' content is the person's data.
2. **Sensitivity and privacy:**
   - the sensitivity is chosen on the way in and carried by every event;
   - an import is forgotten whole by its id;
   - files are read where they are, and nothing is sent anywhere by the import itself;
   - tests use synthetic files only.

## Acceptance criteria

1. Given a WhatsApp export (Android or iOS, day or month first, messages over several lines, system notices), when it is parsed, then each message is one item with its sender, its full text and its time, and the notices are skipped and counted.
2. Given an mbox with a plain-text mail, a multipart mail and an HTML-only mail, when it is parsed, then each mail is one item with its date, sender, subject and readable body.
3. Given an ICS calendar with folded lines, escaped characters and all-day and timed events, when it is parsed, then each event is one item with its start, title, place and description.
4. Given a CSV with a date column, when it is parsed, then each row is one item dated by that column; without one, by the import's time.
5. Given a preview, when it runs, then nothing is written, and the counts and the first new items are returned.
6. Given an import, when it runs again on the same file, then no event is added and every item is counted as a duplicate.
7. Given an import with sensitivity private, then every event is private and is found by `events_search` only with includePrivate.
8. Given an import, when its id is forgotten, then all its events are gone.
9. Given a file over the limits, then nothing is written and the refusal says to split it.
10. Given `filum-mcp`, when `memory_import` previews and then imports a file, then the events are in `.filum/events.jsonl` and `memory_consolidate` hands them to the host.
11. Given the hosted service, when a file is uploaded to `memory/import`, then the events are recorded for the person who uploaded it, and another person sees none of them.

## Open questions

None: the owner delegated the decisions of phase E.
