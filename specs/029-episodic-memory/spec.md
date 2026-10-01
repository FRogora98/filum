# Episodic memory: past conversations, searchable

> Status: Draft
> Area: the turn / tools
> Author/date: federico rogora (drafted by Claude), 2026-10-01
> Roadmap: phase E, product track; asked for by the first LongMemEval run (spec 020)

## Problem

The first LongMemEval run (`oracle`, 20 questions, three small models) measured what the agent's memory loses:
- Filum answered 65–85%, while the raw sessions handed to the same models answered 95–100%.
- The losses are where a summary drops detail: temporal reasoning was 50% for all three models, and knowledge updates 50% with Gemma.

The reason is structural. Filum's memory is what the agent chose to write, a summary, while the conversations themselves are already stored by the hosted product, with their dates, and the agent cannot read them. A person who asks "when did I first mention the bike?" gets what the summary kept, not what was said.

## Goal

The agent has two memories:
- the **curated** one, the files: rules, preferences, facts kept up to date;
- the **episodic** one: every past conversation of the person, searchable with its date.

It answers from both, and LongMemEval measures what that changes.

## Non-goals

- **No new storage:** the conversations are already stored by the host (`conversations`, `conversation_messages`).
- **No embeddings yet:** the search is lexical (BM25, as the benchmark's reference). Semantic retrieval is spec 023, measured against this one.
- **No episodic memory in `filum-mcp`:** there the host agent keeps its own transcripts, which Filum does not see. Its part comes with ingestion (spec 021).
- **No change to the engine's catalog of memory tools.** The new tool belongs to the turn, which owns the conversations.

## Current behavior

- **The turn** (`Filum.Agent/ConversationService`) stores every message with its time. The agent gets the engine's memory tools only.
- **`PlatformInstructions`** say that the memory is the files and the core.
- **LongMemEval, first run** (`evals/reports/…`, spec 020):

| | Gemma | GLM | DeepSeek |
|---|---|---|---|
| Filum | 65% | 85% | 85% |
| raw sessions (BM25, k = 5) | 95% | 100% | 100% |

## Desired behavior

**The tool `conversation_search`:**
- **Parameters:** `query`, plus optional `from` and `to` dates.
- **What it returns:** the person's past messages that match best (BM25 over the messages of every conversation but the current one), up to a limit. Each comes with its date, its conversation's title and the message that followed it (the answer, or the person's reply), so a fact is read in context. A long message is cut to its start and the part around the best-matching word.
- **What it searches:** the person's own conversations only, as every query of the turn.
- **Results are data:** the platform rules already say so for files, and they apply here too.
- **Each call is a step** of the turn, of kind `searched`.

**The instructions** gain one rule. The files are what the agent chose to keep; when a question needs a detail, a date, or what exactly was said, it searches the past conversations, and it says when the two disagree, the newer winning.

**Configuration:** `Agent:EpisodicMemory`, **off by default** until the benchmark shows it helps and the owner decides to turn it on; the hosted product's behavior does not change before that. Off, the tool is not offered.

**Where it lives:** in `Filum.Agent`, as a tool the turn adds beside the engine's (as hosts add theirs). The endpoint groups' contract does not change.

## Platform check

1. **Generic:** yes. Searching one's own past conversations names no domain.
2. **Sensitivity and privacy:**
   - only the person's own conversations are searched;
   - a conversation the person deleted is gone from the search, since deleting a conversation deletes its messages;
   - private memory files are untouched: this tool reads conversations, not files.

## Acceptance criteria

1. Given a person with past conversations, when the agent calls `conversation_search` with words of an old message, then it gets that message with its date, its conversation's title and the message after it, and never another person's.
2. Given `from` and `to`, when it searches, then only messages in that window come back.
3. Given the current conversation, when it searches, then the current conversation's messages are not in the results.
4. Given a deleted conversation, when it searches, then nothing of it comes back.
5. Given `Agent:EpisodicMemory` off, when a turn runs, then the tool is not offered.
6. Given LongMemEval's `oracle` subset of 20 and the three small models, when Filum with episodic memory is played, then the report gives its accuracy beside spec 020's Filum and references, per ability.
7. Given the engine's catalog snapshot, when the tool is added, then it is unchanged.

## Open questions

None.
