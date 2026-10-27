# Changelog

Every change a user of the engine or of `filum-mcp` can notice. Versions follow semantic versioning; the specs named
here are in `specs/`.

## 1.0.0 (2026-10-27): the first public release

The memory and skills engine and `filum-mcp`: a memory folder of plain files with an append-only history (format 3),
facts kept as a ledger of what held when, the hooks that brief the model and keep every message, and 14 tools by default
(33 with `FILUM_TOOLS=full`). What it does is in the README; how it is built in `ARCHITECTURE.md`.

## Pre-release history

Before this repository, the engine was developed in a private one and given to its first users as `filum-mcp` 0.1.0 to
1.1.0. A folder written by those releases is read and moved on to format 3 (see Upgrading in the README). What changed
after 1.1.0 is below, for those users.

### After 1.1.0 (pre-release)

The memory folder's format changes (format 3), and a 1.x engine cannot read what this version writes.

#### Changed
- `memory_log` answers that the words are kept but nothing is saved yet, and points to `fact_record` or a file; the
  instructions say the same in a line of their own (spec 055: models said "saved" after only logging).
- Spec 055:
  - every recall ends with the rules again ("Before you answer, follow: …");
  - the instructions say a rule reaches style and format only, never safety or the host's rules;
  - what the memory does not hold is unknown;
  - promises, closures and lists asked for are saved without being asked;
  - `fact_end` takes `sources`, the events that ended it, kept with the ones it came from;
  - consolidation ends an open item only on an event that says it is done.
- "Now" is today (spec 055): `facts_current` without `on`, the brief and recall show the value that holds today. A
  value recorded to start later is shown apart as planned (`IMemoryFacts.PlannedFactsAsync`), and the one it
  replaces still holds until then.
- `Filum.Evals` runs Claude Code on a config folder of its own: the model sees neither the account running the eval
  nor Claude Code's own memory and CLAUDE.md files, and a check before the first session refuses to run if it does.
- `memory_consolidate` hands the same policies as a hosted pass (the core, rules and open items, what lasts) and
  ends with a last check of every event against the memory (spec 046).
- The brief starts with the rules the person set, and every recall repeats them before what matched the message
  (spec 045). The brief and the instructions say the memory is the source about the person: it wins over anything
  else the context says about them.
- **`events_search` shows conversations, not excerpts** (spec 042): the facts that may answer the query (by its words
  and, with the search model, by its meaning), each with where it came from; the conversation the best facts were said
  in; then the best conversations, as many as the scores warrant (at most `RecallSessionsMax`, 5), each whole when it
  fits its share of `RecallBudgetChars` (24,000 characters), else its matching turns with their neighbours and the
  event to read the rest with `events_read`. The tool's name and parameters are unchanged; its structured content now holds only the hits of the conversations shown.
- **The facts are a ledger** (spec 041): `/facts.csv` gains four columns at the end (`recorded_at`, `ended`, `author`,
  `quote`), added to an older table at its next change; an older engine still reads it. A value ends as `changed`,
  `corrected`, `retracted` or `ended`; a value recorded with an earlier `validFrom` is placed in time instead of closing
  the later one; a value for a day that already has one replaces it there, and the later said wins; `correction` with
  `validFrom` corrects the value of that day; a value placed inside an ended time ends with it, and the next value said
  from an earlier day starts then instead of being added twice; a subject can have several aliases; the same value again changes nothing
  and makes no revision (`MemoryOperation.Unchanged`, a new operation value a host may receive); a private event gives
  no quote. Facts lines show who recorded them (`by the agent`).
- **filum-mcp offers the core profile by default** (spec 040): 14 tools instead of 33 (`memory_overview`,
  `memory_read`, `memory_search`, `events_search`, `events_read`, `memory_write`, `memory_edit`, `memory_append`,
  `memory_consolidate`, `fact_record`, `fact_end`, `facts_current`, `memory_delete`, `memory_log`), and instructions
  that name only those; `facts_history` is in the full profile (`facts_current` with `on` answers what held on a day).
  Tidying is the safety net of what a turn missed, so it is offered by default (spec 046).
  To get every tool back, set `FILUM_TOOLS=full` in the server's configuration, for example
  `claude mcp add filum -e FILUM_TOOLS=full -- /path/to/filum-mcp`.
- **One fact policy** (spec 040): `fact_record` for every point fact that can change, about a person, a place or a
  thing; the core and documents for who the person is and for longer texts. `PlatformInstructions.Text`, a public
  constant, carries the new text (`InstructionParts` holds the shared parts); a host that compiled the old value in
  keeps it until it is rebuilt.
- **The folder store writes format 3** (spec 039): every revision line carries the hash of its content. A folder in
  format 1 or 2 is moved on when opened. A 1.x engine does not check the format: it would show changed files as
  empty. Back up your memory folder before upgrading if you may go back.
- **A damaged history is an error, not a wrong answer** (spec 039): reading a revision whose chain is broken or whose
  content does not match its hash fails with the path and the revision.

#### Added
- `FILUM_TODAY=yyyy-MM-dd`: `filum-mcp` and its hooks take that day as today, for a replay such as a benchmark (spec 055).
- `filum-mcp tidy`, a `Stop` hook (spec 046): when more than 10 messages wait to be tidied, it asks the agent once per
  session to call `memory_consolidate`, after its answer is shown; never twice in a turn. Without hooks,
  `memory_overview` shows the newest of what waits (at most 1,500 characters). The core profile offers `memory_delete`
  again and moves `facts_history` to the full profile (`facts_current` with `on` answers what held on a day).
- No secret is kept, and nothing passing (specs 048 and 049): keys, tokens, private keys and labelled passwords are
  replaced by `[secret removed by Filum]` in every event, import and file the engine writes (facts included); the
  instructions and consolidation keep only what will still matter in a later session (`InstructionParts.Durable`).
- Rules and open items (spec 046): the fact attributes `rule` (a lasting instruction, its reason in the note) and `open`
  (something promised or to do, its due day in the note) hold many values; `fact_end` takes `value` to end one. The
  brief and `memory_overview` show the core, then rules, open items, the other facts, learned, the map and what was
  said, and say when messages wait to be tidied. Consolidation also writes who the person is, rules and open items.
- It knows you from the first message (spec 045): `filum-mcp brief`, a `SessionStart` hook that puts the person's brief
  (core, learned, facts that hold now, the map, how much was said; at most `BriefChars`, 8,000) in the context, and
  `filum-mcp log` now also answers the `UserPromptSubmit` hook with what the memory holds about the message (at most
  `RecallChars`, 1,500). `IMemoryBriefing.BriefAsync` and `RecallAsync` give the same to any host.
- For contributors (spec 044): `CONTRIBUTING.md` with the Developer Certificate of Origin (`DCO`) instead of the CLA,
  `SECURITY.md`, `CODE_OF_CONDUCT.md`, the coding standard in English (`docs/CODING-STANDARD.md`, its Italian twin
  beside it), and CI on Linux, Windows and macOS with a DCO check on pull requests.
- `evals/bench/` (spec 043): Filum on LongMemEval_S and FactConsolidation with your own OpenAI-compatible key (or
  Claude Code), in one command, within a dollar cap, with a result per question and a report with the exact McNemar
  test; `dotnet run --project src/Filum.Evals -- preload` writes LongMemEval histories into memory folders.
- An optional reranker (spec 042): `IReranker` and `FilumEngineBuilder.UseReranker` in the engine; `BgeReranker` and
  `RerankerFiles` (bge-reranker-v2-m3, Apache-2.0, int8 ONNX, about 575 MB, downloaded only on request) in
  `Filum.Embeddings`; `FILUM_RERANKER=on` in `filum-mcp`. `IMemoryFacts.MatchFactsAsync` gives the facts that may
  answer a query; `MemoryOptions` gains `RecallSessionsMax`, `RecallBudgetChars` and `RerankDepth`.
- `fact_end` (spec 041): ends a fact that no longer holds, on a day, or retracts it (`retract`: it never held);
  `fact_record` takes `correction`; `facts_current` takes `on`, the facts that held on a past day; an `alias` fact
  names the same subject (facts, history and search follow it); the structured facts carry the new fields.
- `events_read` (spec 038): the whole conversation of an event, in order, within a budget; `events_search` names each
  match by its event number.
- MCP tool annotations (read-only, destructive, idempotent) from `ToolTraits`, structured content and output schemas for
  `facts_current`, `facts_history` and `events_search`, and refused calls as tool results with `isError` (spec 040).
- Vectors know the model that made them (`IEmbedder.ModelId`, `IMemoryVectorModels`, `.filum/vectors.model`): vectors
  of another model are made again (spec 039).

#### Fixed
- A write interrupted by a crash is completed as the agent's when the folder opens, never adopted as the person's hand
  edit; a temporary file is on disk before it replaces the real one (spec 039).

### 1.1.0 and earlier (pre-release)

The specs in `specs/done/` say what each step added.
