# The memory core — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied). The owner chose to go from this plan straight to code (2026-10-02), with autonomous small commits and a cap of $3 for the LongMemEval measurement.

## Approach

**The projections already exist; the log is what is new.** Today's files with revisions are the projections of the spec: the core, documents, collections and skills, each readable, editable, versioned and undoable. Nothing about them is rewritten. The change adds four things around them:

1. **An event log** in the engine, beside the files, behind the same store: `IMemoryStore` gains three methods (append, read, forget) and every store implements them. These are the memory in a list, the local folder (`.filum/events.jsonl`) and Postgres (`memory_events`). An event is a record: id, occurred and recorded times, kind, source, conversation and message ids, text, sensitivity, the events it came from, and the revisions it produced.
2. **The turn records what happened.**
   - The person's message is a `said` event before any tool runs, and the answer an `answered` event.
   - Each turn that changed the memory adds one `derived` event, listing its revisions with the `said` event as their source. A revision already names its message, so the source of every live write is found from either side.
   - A turn that changed the core's Rules also records a `told` event (an explicit instruction, kept as a fact of the log, sourced on the `said` event).
3. **New tools on the log, and facts with validity.**
   - `events_search` (BM25, date window on `occurred_at`) replaces spec 029's `conversation_search`, and moves from `Filum.Agent` into the engine, so `filum-mcp` has it too.
   - `fact_record`, `facts_current` and `facts_history` keep `/facts.csv`, a collection with a fixed header. Recording a fact closes the open one of the same subject and attribute (`valid_to`) and opens the new one (`valid_from`), in code.
4. **Consolidation**: the same engine tools, run by a model over the events since the last pass, with rules enforced in code.
   - It may add rows, record facts and append dated sections to files that exist.
   - It may not create a file. A new collection, skill or section is a `proposed` event, shown to the agent in the next turns until the person answers (`proposal_answer`).
   - It may not change a file whose latest change is the person's.
   - A pass ends with a `derived` event "consolidated through event N". The pending events are those after it.
   - Who runs it:
     - **Hosted:** a `ConsolidationService` with a cheap model, triggered by a worker (10 minutes of quiet, nightly) and by `POST /memory/consolidate`.
     - **`filum-mcp`:** `memory_consolidate` hands the pending events to the host's model, and the host closes the pass with `through`.

Also:
- **The map is generated.** The memory index gets a summary per file, made in code: a document's first line of text, a collection's fields and row count. The core template loses its hand-kept "Memory map" section, and the instructions stop asking for one.
- **Long conversations** keep their last messages within a character budget. The older part is sent as a summary, made once by the default model and kept as a `derived` event of the conversation. The instructions say `events_search` finds the rest.
- **Forgetting a conversation** (`POST /memory/forget`) removes its events and what was derived only from them: files every revision of which came from those messages, and fact rows sourced only on them. A `corrected` event records it.

**Discarded alternatives:**
- **A new projection store separate from files:** it would duplicate revisions, undo, sensitivity and the app's endpoints, which already work.
- **Facts as their own table:** the owner chose a plain collection the person can open (decisions).
- **Classifying `told` with a model on every message:** it costs a call per message. The core's Rules changing in the turn is a deterministic signal of the same thing.
- **Consolidation in the turn:** it is what makes today's memory slow and lossy. The turn writes live what matters; the rest waits for the pass.
- **Summaries written by consolidation for every file:** a model call per file. The code's first-line summary is enough for routing, and costs nothing.
- **Keeping spec 029's tool in `Filum.Agent`:** `filum-mcp` would lack it. The log lives in the engine.

## Constraints honored

- **Generic in code, personal in data:** event kinds, facts and the consolidation prompt name no domain.
- **The person's files stay theirs.** The log is plain JSON lines in `.filum/` locally. Consolidation's changes are revisions like any other, visible and undoable, and never overwrite the person's edit. Structural changes need the person's yes.
- **One person per memory:** every new store method takes the user, and another user's events do not exist.
- **Stateless services:** the worker holds no state. A pass is guarded per person by a Postgres advisory lock, so two instances never consolidate the same person at once.
- **Deterministic where possible:** fact validity, pending events, the map, the history budget and forgetting are code. Only what to extract is the model's.
- **Cost:** no model call is added to a turn. A turn is one `said` and one `answered` insert, plus a summary call when a conversation first outgrows its budget. Consolidation uses the cheap model, only after a conversation went quiet.

## Contract changes

### Engine

- `MemoryEvent(long Id, DateTimeOffset OccurredAt, DateTimeOffset RecordedAt, string Kind, string Source, Guid? ConversationId, Guid? MessageId, string Text, string Sensitivity, IReadOnlyList<long> Sources, IReadOnlyList<long> Revisions)`.
- `MemoryEventKind`: `said`, `answered`, `imported`, `told`, `corrected`, `derived`, `proposed`. (`proposed` is added to the spec's kinds: a structural change waiting for the person.)
- `MemoryEventSource`: `chat`, `import`, `app`, `agent`, `consolidation`, `host` (`filum-mcp`'s `memory_log`).
- `IMemoryStore`:
  - `AppendEventAsync(userId, NewEvent, ct) → MemoryEvent`;
  - `EventsAsync(userId, EventQuery(afterId, kinds, conversationId, from, before), ct) → list, oldest first`;
  - `ForgetAsync(userId, eventIds, fileIds, ct)`: removes those events, and those files with all their revisions, for good.
- `MemoryService`:
  - `RecordAsync`, `EventsAsync`, `SearchEventsAsync` (BM25);
  - `PendingAsync` (events after the last pass) and `CloseConsolidationAsync(through)`;
  - `RecordFactAsync`, `CurrentFactsAsync`, `FactHistoryAsync`;
  - `ProposalsAsync` and `AnswerProposalAsync`;
  - `ForgetConversationAsync`.
- `MemoryAuthor.Consolidation = "consolidation"`, a new author of revisions.
- `/facts.csv` header: `subject,attribute,value,valid_from,valid_to,sources,note`.
  - `sources`: event ids joined by spaces.
  - Dates: `yyyy-MM-dd`.
  - An open fact has an empty `valid_to`.
- `MemoryOptions`:
  - `MaxHistoryChars` (default 60000): the conversation sent to the model;
  - `EventsSearchMax` (default 8).

### Tools (catalog, snapshot regenerated)

- `events_search(query, from?, to?)`: what was said or imported, with its date and the reply around it; private events only when asked.
- `fact_record(subject, attribute, value, validFrom?, note?)`: closes the open fact of that subject and attribute, and opens this one.
- `facts_current(subject?)`: the open facts.
- `facts_history(subject, attribute?)`: every fact, oldest first, with its dates and sources.
- `proposal_answer(id, accepted)`: records the person's answer to a consolidation proposal. On a yes, the agent then creates what was proposed with the usual tools.

### Removed

- The tool `conversation_search`, the class `EpisodicMemory`, and `Agent:EpisodicMemory`.

### `filum-mcp` (its own tools, not in the shared catalog)

- `memory_log(text, occurredAt?)`: one `said` event, source `host`; writes no projection.
- `memory_consolidate(through?)`:
  - without `through`: the pending events with the consolidation rules, and "call memory_consolidate with through=N when done";
  - with `through`: closes the pass.

### HTTP (additions within v1, OpenAPI snapshot regenerated)

- `POST /memory/consolidate`: runs a pass for the person now; returns `{ events, changes, proposals }`.
- `POST /memory/forget` with body `{ conversationId }`: returns `{ events, files, rows }` removed.

### Configuration

- `Consolidation:Enabled` (default false: a host opts in).
- `Consolidation:Model` (default: the default model; measured in T13, the check model cost eight times more per pass).
- `Consolidation:QuietMinutes` (10).
- `Consolidation:NightlyHourUtc` (3).
- `Consolidation:MaxEvents` (200 per pass).

### Postgres

- The table `memory_events`: `id` identity, `user_id`, `occurred_at`, `recorded_at`, `kind`, `source`, `conversation_id`, `message_id`, `text`, `sensitivity`, `sources bigint[]`, `revisions bigint[]`.
- Index `(user_id, id)`.
- No foreign key to conversations: a deleted conversation's events are removed by `ForgetConversationAsync` or kept by design.
- A host adds its migration (T12).

## Files touched

| File | Change |
|---|---|
| `src/Filum.Engine/MemoryEvents.cs` (new) | event records, kinds, sources, query; BM25 search |
| `src/Filum.Engine/IMemoryStore.cs`, `InMemoryMemoryStore.cs`, `LocalFolderStore.cs` | the three event methods; `.filum/events.jsonl` |
| `src/Filum.Engine/MemoryService.cs` | events, pending, facts, proposals, forget; generated map summaries |
| `src/Filum.Engine/Facts.cs` (new) | the `/facts.csv` rules |
| `src/Filum.Engine/Consolidation.cs` (new) | the consolidation prompt and the rules a pass's tools follow |
| `src/Filum.Engine/MemoryTools.cs` | the five new tools; a consolidation mode (refusals in code) |
| `src/Filum.Engine/PlatformInstructions.cs`, `MemoryConstants.cs`, `MemoryOptions.cs`, `MemoryModels.cs` | no hand-kept map; events rules; options; author |
| `src/Filum.Agent/ConversationService.cs` | said/answered/derived/told events; history budget and summary; proposals in the instructions |
| `src/Filum.Agent/ConsolidationService.cs`, `ConsolidationWorker.cs` (new) | the hosted pass and its trigger |
| `src/Filum.Agent/EpisodicMemory.cs`, `Hosting.cs` | removed; the option removed |
| `src/Filum.Agent/Data/*` | `MemoryEventRow`, the table in `FilumModel`, `PostgresMemoryStore` |
| `src/Filum.Agent.Http/FilumEndpoints.cs`, `FilumAgentServices.cs` | the two endpoints; consolidation services |
| `src/Filum.Mcp/*` | `memory_log`, `memory_consolidate`, instructions |
| `src/Filum.Evals/LongMemEval/*` | `--consolidate`: a pass before the question |
| tests | contract tests for events on every store; facts; consolidation rules; turn events; forget; MCP tools; snapshots |

## Risks and mitigations

- **The log grows without bound.** Its text is what was said, the same size as the conversations already kept. Forgetting removes it.
- **Consolidation writes nonsense.** Every change is a revision by `consolidation`, undoable. It cannot create files nor overwrite the person's edits. It is off unless the host enables it.
- **Cost of consolidation.** One cheap-model run per quiet conversation, capped at `MaxEvents`. The usage is recorded like a turn's.
- **Hard deletion is irreversible.** It is only on the person's explicit call, removes only what came from that conversation alone, and reports what it removed.
- **Postgres schema change.** Only a new table. Hosts pinned to v0.3.0 are unaffected until they upgrade and add the migration.
- **Rollback:** each task is a commit. Consolidation is off by config, and `events_search` can be left out with `Memory:ExcludedTools`.

## Test strategy

| Criterion | Test |
|---|---|
| 1 `said`/`answered` | `Filum.Agent.Tests`: a turn on the sample host with a fake model; the said event exists before the first tool call (the fake model's tool reads it with `events_search`) |
| 2 known next message, next chat | `Filum.Agent.Tests`: turn 1 writes the core (scripted); turn 2 in the same chat gets turn 1 in its messages; turn 1 of a new chat gets the core in its instructions |
| 3 `told` | `Filum.Agent.Tests`: a turn that edits the core's Rules records `told`, sourced on `said` |
| 4 consolidation with sources | `Filum.Agent.Tests`: `POST /memory/consolidate` with a scripted model that records a fact; a `derived` event lists its revision; undo works |
| 5 fact validity | `Filum.Engine.Tests`: two `fact_record` calls; `facts_current` gives the new value, `facts_history` both |
| 6 proposals | `Filum.Engine.Tests`: a consolidation-mode tool refuses a new file and records a proposal; nothing is created until `proposal_answer` |
| 7 the person's edit wins | `Filum.Engine.Tests`: consolidation-mode change to a file last changed by the person is refused |
| 8 generated map | `Filum.Engine.Tests`: the index has one line per file with its summary; the core template has no map |
| 9 forget | `Filum.Agent.Tests`: forget a conversation; its events, the file made only from it and its fact rows are gone; a `corrected` event remains |
| 10 `memory_log` | `Filum.Mcp.Tests`: the real server appends to `.filum/events.jsonl`, and no file changes |
| 11 `memory_consolidate` | `Filum.Mcp.Tests`: pending events returned; after `through`, none pending |
| 12 LongMemEval | by hand: the runner on `oracle` and a few `S` questions, cap $3, report committed |
| 13 long conversations | `Filum.Agent.Tests`: a conversation over the budget sends a summary plus the last messages; `events_search` finds the first message |
| events on every store | `MemoryServiceContract`: append, query, forget, user isolation, on memory, the local folder and Postgres |

What cannot be tested offline: whether a real model writes live what matters, and what a real consolidation extracts. The LongMemEval run (T13) measures both.
