# Absorb: imports from files — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (delegated by the owner).

## Approach

**Parsers in the engine, one pipeline after them.** `Imports.Parse(format, fileName, content)` turns a file into items: when, who, text, identity. It handles the four formats with no new dependency: line rules, RFC 2822 dates, MIME parts decoded by hand, ICS unfolding. Then `MemoryService.ImportAsync`:

1. checks the limits;
2. drops the items whose identity is already in the log;
3. in a preview, stops there and returns counts and samples;
4. otherwise appends all the new events at once.

**Identity without a new column.** An item's identity (for example a mail's `Message-ID`, or a WhatsApp message's chat, time, sender and text) is hashed into a deterministic `Guid` stored as the event's `message_id`. The file's source id (format and file name) is hashed the same way into its `conversation_id`. So:
- duplicates are found by message id;
- an import is forgotten by spec 030's `ForgetConversationAsync`;
- the log's schema does not change.

**A batch append.** `IMemoryStore.AppendEventsAsync` writes many events at once (one insert in Postgres, one append in the local folder), so 5,000 items do not cost 5,000 round trips.

**Discarded:**
- **An import table, or an identity column:** the event already has two ids that fit, and the log stays one table.
- **A library for mail or calendars:** the subset needed (plain text bodies, basic MIME, VEVENT) is small and testable, and a dependency would ship in every host.
- **A model choosing what to keep at import:** it costs a call per item. Consolidation reads the events afterwards, and spec 022 will gate them.

## Constraints honored

- **Generic:** formats, not domains.
- **The person's data stays theirs:** `filum-mcp` reads local files and sends nothing; the hosted endpoint stores events only for the person who uploaded.
- **Sensitivity chosen on the way in:** every event of an import carries it.

## Contract changes

- **Engine:**
  - `ImportItem(DateTimeOffset? When, string? Who, string Text, string Identity)`;
  - `ImportFormat` (`whatsapp`, `mbox`, `ics`, `csv`);
  - `Imports.Parse` and `Imports.Detect`;
  - `ImportRequest(string FileName, string Content, string? Format, string Sensitivity, bool Preview)`;
  - `ImportResult(string Format, Guid SourceId, int Items, int New, int Duplicates, int Skipped, IReadOnlyList<string> Sample, bool Written)`;
  - `MemoryService.ImportAsync`;
  - `MemoryOptions.MaxImportItems` (5,000) and `MaxImportBytes` (20 MB).
- **Store:** `IMemoryStore.AppendEventsAsync(userId, IReadOnlyList<NewMemoryEvent>, now)`.
- **`filum-mcp`:** the tool `memory_import(path, format?, sensitivity?, preview = true)`.
- **HTTP (an addition within v1):** `POST …/memory/import`, multipart form `file`, `format`, `sensitivity`, `preview`. It returns `ImportResultDto`, the same fields as `ImportResult`.

## Files touched

| File | Change |
|---|---|
| `src/Filum.Engine/Imports.cs` (new) | the four parsers, detection, identity |
| `src/Filum.Engine/MemoryService.Imports.cs` (new) | the import |
| `src/Filum.Engine/IMemoryStore.cs`, `InMemoryMemoryStore.cs`, `LocalFolderStore.cs`, the hosting service's Postgres store | the batch append |
| `src/Filum.Mcp/McpServerSetup.cs`, `McpInstructions.cs` | `memory_import` |
| the hosting service's HTTP layer, `Dtos.cs` | the endpoint |
| tests | parsers; the contract (preview, duplicates, sensitivity, forget, limits, batch); MCP; the endpoint; OpenAPI snapshot |

## Risks and mitigations

- **A big import drives consolidation's cost:** 5,000 items make about 25 passes of 200, about $0.05 with Gemma. This is the default model's estimate, measured later.
- **A parser misreads a file:** the preview shows the first items before anything is written, and an import is forgotten whole by its id.
- **Rollback:** the commits; no schema change.

## Test strategy

| Criteria | Test |
|---|---|
| 1–4 | `ImportsTests` on synthetic files |
| 5–9 | `MemoryServiceContract`, on every store |
| 10 | `Filum.Mcp.Tests` over stdio |
| 11 | the hosting service's tests through the sample host |
