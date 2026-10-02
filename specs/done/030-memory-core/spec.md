# The memory core: an event log and the projections the agent builds

> Status: Implemented (2026-10-02). Reviewed (the owner agreed with every proposed answer, 2026-10-01: "a me tornano tutte le cose che mi dici")
> Area: engine (the memory core) / the turn
> Author/date: federico rogora (drafted by Claude), 2026-10-01
> Roadmap: phase E; the owner's decision of 2026-10-01 to rethink the core from its architecture, keeping the frame ("il nucleo è il vero motore dell'engine, se cambiamo quello il resto può rimanere")

## Problem

Filum's memory today is what the agent writes while it answers: a summary, written in the turn. The first LongMemEval run (spec 020) measured both costs of that design:
- **It loses.** Small models answered 65–85% from Filum's memory, against 95–100% from the raw sessions. The losses are dates, numbers and the old version of a fact that changed.
- **It is expensive.** Every piece of information goes through a full agent turn with tool calls, about 15 times the cost of keeping the sessions.

Searching the stored conversations (spec 029) took Gemma from 65% to 80%: what was said, kept, is the strongest memory there is.

The owner's own personal agent, the model for Filum's behavior, shows the same shape from the inside (its own account, 2026-10-01):
- it keeps no original conversations, only what it extracted, so a bad extraction is lost for good;
- it has no consolidation, so outdated facts stay until someone happens on them;
- its routing table ("topic → file") is kept by hand, so a file without a row is a lost file;
- its state files grow with no summary to read instead;
- stable facts land in the diary instead of the profile.

What it gets right is what Filum must keep:
- the agent creates its own structures (tables with a schema and a free `note` column), procedures and scripts;
- dated, append-only history;
- state kept apart from history;
- every answer cites its source.

## Goal

A memory where nothing said is ever lost, and the agent's own structures are built from it, can be rebuilt from it, and say where they come from:
- **an event log**, append-only, the single source of truth;
- **projections** the agent creates and evolves on top of it: the core, facts that hold for a time, collections, documents, skills, and later functions.

It behaves as the owner's personal agent does, without its losses: it adapts to the person, extracts and updates its structures live when a message calls for it, and creates its own collections, tasks and procedures (the owner, 2026-10-01: "un sistema che agisca esattamente COME personal agent, ma non necessariamente nello stesso modo"). Cost matters, but it is one goal among others, not the main one. The frame stays as it is: hosting, endpoints, `filum-mcp`, evals, packages, the claim check.

## Non-goals

- **No change to the frame:** hosting (017), the endpoint contract (019), packages (016), the eval runner (018, 020) and `filum-mcp`'s install stay. What changes is what they carry.
- **No embeddings in this spec:** retrieval stays lexical with time filters. Hybrid retrieval (023) and the fast gate (022) plug into the read path afterwards and are measured against it.
- **No functions yet:** the agent's own code (capability 5) is a later projection kind. The log and the projection model are designed so that it fits.
- **No external connectors:** they feed the log later (024). This spec needs only conversations and imports from files.

## Current behavior

- **`MemoryService`:** files with revisions, written by the agent's tools during the turn.
- **The core `/filum.md`:** always in the instructions, with an index of every file.
- **Conversations** are stored by the hosted turn, searchable since 029 (off by default). `filum-mcp` sees no conversation.
- **Consolidation:** none. Facts are overwritten in place or appended, and the current value of a changing fact is whatever the last write left.

## Desired behavior

### 1. The event log: what happened, never lost

Every person has one append-only log of events. An event has:
- an id;
- **when it happened** (`occurred_at`) and **when it was recorded** (`recorded_at`): an imported mail or a past session happened before it was recorded;
- its **kind**, its **source** (a chat and its message, an import and its item, the person in the app, the agent, the consolidation);
- its text or data;
- its sensitivity.

The kinds:

| Kind | Example |
|---|---|
| `said` | a message of the person |
| `answered` | the agent's answer |
| `imported` | an item from a file or, later, a connector (a chat export, a mail, a calendar entry) |
| `told` | an explicit instruction: "remember…", "from now on…", "forget…" |
| `corrected` | the person changes, hides, deletes or undoes something in the app |
| `derived` | a change to a projection, with the events it came from |

Nothing in the log is ever rewritten. Deleting is a `corrected` event, and a real deletion (the person's right to be forgotten) removes the events it names and everything derived from them, and says so.

### 2. Projections: what the agent builds

A projection is a structure derived from events, readable and editable as today's files are, and versioned as they are (revisions, undo). Each one carries:
- its kind;
- **a summary**, a few lines read instead of the whole projection when that is enough;
- the events it was derived from.

| Projection | What it holds | Lesson behind it |
|---|---|---|
| **core** | who the person is, their rules, how to answer | stable facts go here, not in a diary |
| **facts** | statements that hold for a time, kept as a plain collection `/facts.csv` (subject, attribute, value, `valid_from`, `valid_to`, sources, note) that the person can open and edit like any other. The current value is a query, not an overwrite; an update closes the old fact and opens a new one | the 4-plants-not-3 case; "Stato al …" sections that contradict each other |
| **collections** | tables with a schema the agent chose, a free `note` column always, a date on each row, the source of each row | the personal agent's CSVs |
| **documents** | narrative notes with dated sections | decisions, plans, reflections |
| **skills** | procedures, as today | `/spesa`, `/sprint` |
| *(later)* **functions** | the agent's own sandboxed code | the personal agent's scripts |

**The map** ("topic → projection") is generated from the projections' summaries, never kept by hand. With the core and the enabled skills, it is what the agent always reads.

### 3. Writing: live when it matters, never lost, tidied afterwards

- **Recorded, always:** every message is an event, before anything else runs. Nothing is lost if the agent writes nothing, or writes badly.
- **Known from the next message on:** what the person said is never waiting for consolidation. In the same conversation every earlier message is part of the turn (as today: the whole conversation is sent); in a new conversation it is known because the turn wrote it. "My name is …" is known at the next message and in the next chat.
- **Live, when the message calls for it,** as the personal agent does:
  - the agent updates its projections in the turn: a new fact (closing the one it replaces), a row, a task, a dated section, a rule in the core, a new collection or skill it proposes;
  - every such write cites the events it comes from;
  - the claim check verifies that what was claimed was done.

  "When it calls for it" is the agent's judgment, guided by the instructions: a lasting instruction, a correction, a fact about the person or their life, a request to keep something. Small talk writes nothing.
- **Consolidation** is the safety net, and the part the personal agent lacks:
  - **When:** after 10 minutes without a new message in a conversation, in a nightly pass, and on demand. It reads the events since its last pass. Its timing changes nothing the agent knows, only how tidy it is.
  - **What it does:**
    - catches what the turn did not write: turns events into facts (closing the facts they replace), rows and dated sections;
    - refreshes summaries and the map;
    - merges duplicates;
    - flags contradictions it cannot settle.
  - **Who runs it:** in the hosted product, a cheap model (later the fast gate where it suffices). In `filum-mcp`, which has no model of its own, the host's model, when Filum asks it to (§5).
  - **How it is seen:** every change is a `derived` event, visible in the history and undoable.
  - **Structural changes** (a new collection, a new skill, a new section of the core) are **proposed** to the person, as skills are today. Rows, facts and summaries are applied directly. The personal agent's rule, "when I don't know where something goes, I ask", becomes a property of the system.
- **Long conversations:** past a length the model cannot read whole, the oldest messages of the conversation are sent as a summary instead. They stay, every one, in the log, and `events_search` finds them.
- **A projection edited by the person** records a `corrected` event: their version wins, and consolidation never overwrites it.

### 4. Reading: route, then cite

- **The agent always reads** the core, the map and the enabled skills.
- **Tools, by what they answer:**
  - `memory_overview`, as today, from the map;
  - `facts_current` and `facts_history`: what holds now, and how it changed;
  - `events_search`: what was said or imported, with time filters on `occurred_at`. It replaces and extends `conversation_search`;
  - the projection tools of today: read a projection, aggregate a collection exactly.
- **Every answer that states a remembered fact names its source** (a projection or an event) in the turn's steps. The claim check learns to ask "is every remembered claim backed by a step?", the personal agent's "always cite the source file", checked in code.

### 5. The two products

- **The hosted product:** the log is a table next to the projections, fed by the turn and by imports.
- **`filum-mcp`:** Filum has no model and sees no conversation; the host's model does all the thinking, through Filum's tools.
  - The log is an append-only file in the folder (`.filum/events.jsonl`), next to the projections, which are today's files.
  - **Live writes** are the host's model calling the projection tools, as today.
  - **`memory_log`** records what the person said that matters, as the host's model chooses it: an event appended, no projection written.
  - **`memory_consolidate`** hands the host's model the events not yet consolidated, with what consolidation should do (§3); the model does it with the usual tools. The MCP instructions ask it to call it at the start of a session when events are pending, and whenever the person asks. Its cost is the host's, like everything else.
  - The instructions ask the host to log what matters; imports from files feed the log as in the hosted product; the growth test measures how often hosts comply.
  - MCP sampling (the server asking the client to run a model) is not relied on: support for it varies across clients.

### 6. It replaces today's core

Filum is still in development: nothing has been released as a stable memory. The core described here takes the place of today's, piece by piece, and each piece removes the code it replaces in the same change. There is no compatibility layer and no period with two cores. The frame keeps working at every commit.

### 7. How it is decided

On LongMemEval (spec 020), on `oracle` and on `S`, the same subsets and models:
- **The systems:** this core, the references (no memory, naive retrieval over the raw sessions), and the measurements of spec 020 as the starting point.
- **Ablations:** without consolidation, without fact validity, without the generated map.
- **The target:** close to the raw sessions (95–100% on `oracle` in spec 020), at least on knowledge updates and temporal questions, at a reasonable cost per question. Below it, the design is rethought before more is built on it.

## Platform check

1. **Generic:** yes. Events, facts, collections, documents and skills name no domain. A domain's structures are what an agent builds from a person's events, or what a package seeds.
2. **Sensitivity and privacy:**
   - events carry sensitivity, and every projection derived from a private event is private;
   - a real deletion follows the sources down to every projection built from them, which a lossy memory could not do;
   - nothing personal enters this repository.

## Acceptance criteria

1. Given a turn, when the person writes, then an event `said` is recorded with its `occurred_at`, before any tool runs, and the answer an `answered` event.
2. Given a person who says "my name is Sam", when they write again in the same conversation, and when they open a new conversation right after, then the agent knows the name both times, with no consolidation run in between.
3. Given an explicit instruction ("from now on…"), when the turn ends, then the core holds it, a `told` event is its source, and the claim check verified it.
4. Given a conversation that ended, when consolidation runs, then its new facts, rows and dated sections exist with their source events, each change a `derived` event, undoable.
5. Given a fact that changes ("now 4, not 3"), when consolidation sees the new event, then the old fact gets `valid_to` and the new one `valid_from`; `facts_current` gives 4, and `facts_history` gives both with their sources.
6. Given a kind of information with no home, when consolidation would create a collection, then it proposes it to the person, and nothing is created until the person accepts.
7. Given a projection the person edited, when consolidation runs again, then the person's version stays.
8. Given any projection, when the map is read, then it has one line per projection with its summary, generated, never hand-kept.
9. Given a person who deletes a source for good, when the deletion runs, then its events and everything derived only from them are gone, and the history records the deletion.
10. Given `filum-mcp`, when the host calls `memory_log`, then an event is appended to `.filum/events.jsonl` and no projection is written in the turn.
11. Given `filum-mcp` with events not yet consolidated, when the host calls `memory_consolidate`, then it gets those events and the instructions to consolidate them, and once the host has written, the next call returns nothing pending.
12. Given LongMemEval `oracle` and `S`, when this core and the references are played on the same subsets and models, then the report shows accuracy per ability and cost per question for each, with the ablations, and the target of §7 decides.
13. Given a conversation longer than the model can read, when the person asks about its first messages, then the agent finds them through `events_search`, and nothing of the conversation is missing from the log.

## Decisions

All the open questions were answered by the owner on 2026-10-01 ("a me tornano tutte le cose che mi dici"):
- **The memory kept so far while dogfooding:** dropped when the new core is deployed (it is almost empty).
- **When consolidation runs:** after 10 minutes without a new message, nightly, and on demand (§3).
- **Proposed or applied:** collections, skills and core sections are proposed; facts, rows, dated sections and summaries are applied, all undoable (§3).
- **Facts:** a plain collection, `/facts.csv` (§2).
- **`filum-mcp`:** the host's model logs with `memory_log` and consolidates with `memory_consolidate` when Filum asks it (§5).
- **Long conversations:** the oldest messages are summarized for the model, and all of them stay in the log (§3).

## Settled while planning (2026-10-02)

The plan fixes these details; they change no acceptance criterion:
- **`proposed`** is one more event kind: a structural change consolidation suggests, shown to the agent until the person answers (`proposal_answer`).
- **`told`** is recorded when a turn changed the core's Rules, sourced on the person's `said` event: a deterministic signal, with no model call per message.
- **Summaries in the map** are made in code: a document's first line of text, a collection's fields and rows. No model call per file.
- **The claim check on remembered facts** (§4: "is every remembered claim backed by a step?") is left for later: it would add a model call to every answer, and no acceptance criterion needs it. The instructions keep asking the agent to name its source.

## Result (2026-10-02)

Every acceptance criterion has its test, except criterion 12 in part: the ablations did not fit in the measurement's cap ($3) and were not run.

LongMemEval (`evals/reports/20261002-170349/`, one repetition, judge gpt-5.4-mini):

| | Gemma 4 31B | DeepSeek V4 Flash |
|---|---|---|
| `oracle`, 20 questions, this core | **85%** (knowledge updates 50%, temporal 100%) | **95%** (knowledge updates 100%, temporal 75%) |
| `oracle`, the old core (spec 020) | 65% | 85% |
| `oracle`, the old core with episodic memory (029) | 80% | 95% |
| `oracle`, the raw sessions (naive BM25, spec 020) | 95% | 100% |
| `S`, 5 questions, this core | **5 of 5** | — |
| `S`, the same 5, the raw sessions (naive BM25) | 3 of 5 (missed multi-session and temporal) | — |

- **Against the target of §7:** on `oracle` the core comes close to the raw sessions with DeepSeek, not yet with Gemma on knowledge updates (2 of 4). On `S`, where the raw sessions drown in about 50 sessions of noise, it answered all five; five questions are an indication, not a measure.
- **Cost:** about $0.015 per `oracle` question with Gemma (about $0.002 per message: most of it is the turn's input, about 19,000 tokens of instructions, tools and map), $0.28 per `S` question. A consolidation pass with Gemma costs about $0.002; with the check model it cost eight times more, so passes use the default model.
- **Next:** knowledge updates with small models; the cost of a turn (prompt caching, fewer tools per turn, shorter descriptions); more `S` questions and the ablations, with repetitions.
