# It knows you from the first message

> Status: Implemented (2026-10-19). Reviewed (the owner, 2026-10-19: "figo cosi, mi piace, vai parti pure", after the comparison with a
> personal agent that keeps its own memory in files)
> Area: engine (briefing and recall), filum-mcp (instructions, the `log` hook)
> Author/date: federico rogora (drafted by Claude), 2026-10-19

## Problem

An agent with `filum-mcp` knows nothing about the person until it calls a tool. The instructions say "call
memory_overview when you start helping"; a model that does not, or calls it late, answers the first message as a
stranger. Later on it remembers only when it decides to search (`events_search`, `facts_current`). A turn where it does
not think to look loses what the memory holds.

A personal agent that feels like it knows its person does it with no clever machinery. A small profile (who they are,
their rules, where things are) is in the context before the first turn, and the model always knows the memory exists.

## Goal

From the first message of every session, the agent knows who the person is and what the memory holds, with no tool
call. On every message, what the memory holds about that message reaches the agent even when it does not search.

## Non-goals

- No model call inside Filum: the brief and the recall are deterministic (the engine still has no model).
- No change to the tools or their catalog.
- Open items, rules with their reason, and a "knows who you are" evaluation are later specs.

## Desired behavior

1. **A brief.** `IMemoryService.BriefAsync(userId, maxChars)` gives a text of at most `maxChars` (default
   `MemoryOptions.BriefChars`, 8,000 characters, about 2,000 tokens). Its sections come in this order, each cut to its
   share, and a section with nothing to say is left out:
   1. **The person's core** (`/filum.md`): who they are, their rules, how they want to be answered.
   2. **What was learned** (`/learned.md`): its latest lines.
   3. **Facts that hold now**: the most recently recorded first, one line each, then how many more there are.
   4. **What the memory holds**: the map of the files, one line each.
   5. **What was said**: how many messages are kept, from when to when, and that `events_search` finds them.

   Nothing private is in it.
2. **`filum-mcp brief`, a hook for the start of a session.**
   - It writes the brief as Claude Code's `SessionStart` hook answer (`hookSpecificOutput.additionalContext`), so the
     person is in the context before the first message, with no tool call.
   - The brief does not go in the MCP instructions: Claude Code cuts server instructions at 4,096 characters, and
     filum-mcp's own instructions are already 3,098 (core profile) and 4,075 (every tool).
   - A test keeps the instructions of both profiles under 4,096 characters.
   - Hosts without hooks keep the instructions' first step, `memory_overview`.
   - Like `log`, it never fails a session: on any error it writes nothing and exits 0.
3. **A recall.** `IMemoryService.RecallAsync(userId, message, maxChars)` gives what the memory holds about one message,
   at most `maxChars` (default `MemoryOptions.RecallChars`, 1,500 characters):
   - the facts that may answer it (spec 042's matching, words only);
   - the best few things said that match it, each cut short, with its date and event number.

   It is empty when nothing matches. It never includes private content.
4. **The `log` hook answers with the recall.**
   - `filum-mcp log` (Claude Code's `UserPromptSubmit` hook) still keeps the message in the log.
   - It now also answers the hook with the recall of the message, as the hook's added context. The recall is found
     before the message is kept, so the message never recalls itself.
   - A plain-text input (another host) gets no answer, as before.
   - A failure still never blocks the message: nothing is answered, and the reason goes on stderr.
5. **The README** shows both hooks (`SessionStart` with `filum-mcp brief`, `UserPromptSubmit` with `filum-mcp log`),
   and says what each host gets: Claude Code with the hooks gets the person at start and the recall on every message;
   other hosts get the memory through `memory_overview` and the tools, as before.

## Amended after the second Track A round (2026-10-19)

On the first person, with Haiku 4.5 and the hooks, rules that were in the memory were not followed, and a session
without hooks answered from the host's account ("I see your email") instead of the memory.
- **The brief starts with the rules,** under "follow every one in this answer", before the core.
- **Every recall repeats the rules that hold,** within 40% of its budget, before what matched the message; a message
  that matches nothing still gets them.
- **The brief and the instructions say the memory is the source about the person:** it wins over anything else the
  context says about them.

## Platform check

1. **Generic:** any memory and any person. The sections and the ranking name no domain.
2. **Sensitivity:**
   - private files, private facts and private events are never in the brief or the recall;
   - the brief and the recall are read only, and change nothing.

## Acceptance criteria

1. Given a memory with a core, learned lines, facts, files and messages, the brief has the five sections in order. It
   is at most `maxChars`, and the core comes first and whole when it fits.
2. Given more facts than fit, the brief shows the most recently recorded and says how many more hold.
3. Given private files and private messages, neither is in the brief nor the recall.
4. Given a fact and a message that match a question, the recall shows both. Given a question that matches nothing, it
   is empty.
5. `filum-mcp brief` writes the hook answer with the brief of its memory. With a broken folder it exits 0 with nothing
   on stdout. filum-mcp's instructions are under 4,096 characters in both profiles (a test).
6. `filum-mcp log` with a hook's JSON keeps the message. It writes on stdout the hook answer with the recall
   (`hookSpecificOutput.additionalContext`), which does not contain the message itself. With a plain-text input it
   writes nothing on stdout. With a broken folder it exits 0 with nothing on stdout.
7. The tool catalog's snapshot does not change.
