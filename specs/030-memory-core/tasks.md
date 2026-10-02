# The memory core — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — The event log in the engine**
    - Do: `MemoryEvents.cs` (records, kinds, sources, query, BM25 over events); the three `IMemoryStore` methods in `InMemoryMemoryStore` and `LocalFolderStore` (`.filum/events.jsonl`, append-only; forget rewrites it without the forgotten lines); `MemoryService.RecordAsync`, `EventsAsync`, `SearchEventsAsync`.
    - Verify: `MemoryServiceContract` gains the event tests (append and read in order, filters, isolation, forget); `dotnet test tests/Filum.Engine.Tests`.

- [x] **T2 — The event log in Postgres**
    - Do: `MemoryEventRow` and `memory_events` in `FilumModel`; `PostgresMemoryStore` implements the three methods; the contract runs on it.
    - Verify: `dotnet test tests/Filum.Agent.Tests` (the contract on Postgres).

- [x] **T3 — The turn records what happened**
    - Do: `ConversationService` records `said` before the model, `answered` after; one `derived` event per turn that wrote (its revisions, source the `said`); `told` when the turn changed the core's Rules section.
    - Verify: new `EventLogTests` (criteria 1, 2, 3).

- [ ] **T4 — `events_search` replaces `conversation_search`**
    - Do: the tool in `MemoryTools` over `SearchEventsAsync` (said, answered, imported; date window; private only when asked); the instructions' rule; remove `EpisodicMemory`, `AgentOptions.EpisodicMemory` and their tests; snapshot.
    - Verify: tool catalog snapshot reviewed; `EventLogTests` find a past message with dates; all suites green.

- [ ] **T5 — Facts with validity**
    - Do: `Facts.cs`; `MemoryService.RecordFactAsync` (closes the open fact of subject and attribute, opens the new one, sources), `CurrentFactsAsync`, `FactHistoryAsync`; tools `fact_record`, `facts_current`, `facts_history`; a line in the instructions.
    - Verify: `FactsTests` (criterion 5) on memory and the local folder; snapshot.

- [ ] **T6 — The generated map**
    - Do: `BuildIndexAsync` adds each file's summary (a document's first line of text, a collection's fields and rows); the core template and the instructions (hosted and MCP) lose the hand-kept memory map.
    - Verify: `MapTests` (criterion 8); existing tests updated where they quoted the old template.

- [ ] **T7 — Consolidation's rules in the engine**
    - Do: `Consolidation.cs` (the prompt; pending = events after the last pass); `MemoryTools` in consolidation mode (author `consolidation`; no new files: a `proposed` event instead; no change to a file whose latest revision is the person's); `ProposalsAsync`, `AnswerProposalAsync`, tool `proposal_answer`; open proposals in the turn's instructions.
    - Verify: `ConsolidationRulesTests` (criteria 6, 7); snapshot.

- [ ] **T8 — Consolidation in the hosted product**
    - Do: `ConsolidationService` (a run of the cheap model with the consolidation tools over the pending events, usage recorded, the pass closed); `ConsolidationWorker` (opt-in; quiet minutes, nightly hour, advisory lock per person); `POST /memory/consolidate`; OpenAPI snapshot.
    - Verify: `ConsolidationTests` with a scripted model (criterion 4); OpenAPI snapshot reviewed.

- [ ] **T9 — Long conversations**
    - Do: the history budget (`MaxHistoryChars`): the last messages that fit, the older part as a summary (check model, kept as a `derived` event of the conversation, made again when the hidden part has grown by half), and the rule that `events_search` finds the rest.
    - Verify: `LongConversationTests` (criterion 13).

- [ ] **T10 — Forgetting a conversation**
    - Do: `MemoryService.ForgetConversationAsync` (its events; the files all of whose revisions came from its messages; the fact rows sourced only on its events, removed as a revision; a `corrected` event); `POST /memory/forget`; OpenAPI snapshot.
    - Verify: `ForgetTests` (criterion 9).

- [ ] **T11 — `filum-mcp`: `memory_log` and `memory_consolidate`**
    - Do: the two tools in `McpServerSetup`; the MCP instructions (log what matters; consolidate at the start of a session when events are pending, and when asked).
    - Verify: `Filum.Mcp.Tests` over stdio (criteria 10, 11); `scripts/smoke-mcp.sh`.

- [ ] **T12 — The private host**
    - Do: in the private repo, after the submodule update: the migration of `memory_events`, `Consolidation:Enabled` true in the service's settings, the build and tests.
    - Verify: `dotnet build` and `dotnet test` of the private solution.

- [ ] **T13 — The measurement (cap $3)**
    - Do: the runner's `--consolidate` (a pass through the host's endpoint before the question); on `oracle`, the 20 questions on the new core with the three small models; with what is left of the cap, a few `S` questions on one model; the report committed. Ablations only within the cap, said in the report.
    - Verify: the report under `evals/reports/`; criterion 12; the total spent stated.

- [ ] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion against the code and the tests; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update the index in `specs/README.md`; README and `CLAUDE.md`; the roadmap in the private repo; spec 029 closed as replaced.
    - Verify: `dotnet build Filum.slnx`, `dotnet test Filum.slnx`, `bash scripts/check-secrets.sh`; the private repo's commands.
