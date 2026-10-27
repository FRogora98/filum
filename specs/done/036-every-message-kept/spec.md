# Every message kept, whatever the model

> Status: Implemented (2026-10-06). Reviewed (the owner, 2026-10-06, on the proposal "a hook that logs every message of the person, whatever the model": "vai prosegui", then "andiamo avanti")
> Area: MCP (`filum-mcp`), evals
> Author/date: federico rogora (drafted by Claude), 2026-10-06

## Problem

With `filum-mcp`, what the person says enters the memory only when the host's model decides to keep it (`memory_log`, or a file). On LongMemEval `oracle` (20 questions, Claude Code with Haiku 4.5 and a consolidation session after each history session), Filum answered 9 of 19. In 8 of the 10 failures the answer was "I have no record of it".

A memory folder kept from one question shows why:
- the person said "Premier Silver", then months later "Premier Gold";
- the model kept one file and overwrote Silver with Gold;
- it logged one unrelated event;
- consolidation had nothing to recover from.

A hosted product does not have this weakness: every message is recorded as an event by code.

## Goal

With one line of configuration in the host, every message the person sends is in the log of events before the model reads it. Nothing depends on the model's diligence, so consolidation and search can recover what the model missed.

## Non-goals

- **No answer logging:** the host's answers are the model's, and the person's words are what memory needs first.
- **No host other than those with hooks:** Claude Code has them; other hosts keep `memory_log`.
- **No change to the tools.**

## Desired behavior

1. **`filum-mcp log [--home <folder>]`** reads stdin.
   - If stdin is a host hook's JSON with a `prompt`, that prompt is the text; otherwise the whole input is.
   - The text is recorded as `said` events (source `host`), in parts of at most 8,000 characters cut at line ends. The parts carry one conversation id, derived from the hook's `session_id` when there is one.
   - It writes nothing to stdout, because a hook's output can reach the model, and it always exits 0: a failure is a line on stderr, never a blocked message.
   - The folder is `--home`, else the server's own (`FILUM_HOME`, else the default). It is locked per call, as by the server, so both can run at once.
2. **The README** gives the Claude Code hook: `UserPromptSubmit` running `filum-mcp log`, with the same home as the server.
3. **The evals:** `--hook` (LongMemEval with `--claude filum`, and the `mcp` growth test) runs Claude Code with that hook (`--settings`), so the gain is measured.

## Platform check

1. **Generic:** yes. It logs the person's words, whatever their domain.
2. **Privacy:** it is local, written to the person's folder, and forgotten like any event.

## Acceptance criteria

1. Given a hook's JSON on stdin, when `filum-mcp log --home <folder>` runs, then the prompt is one `said` event with source `host`, nothing is written to stdout, and the exit code is 0.
2. Given text over 8,000 characters, then it is recorded in parts under one conversation id, and nothing is lost.
3. Given a folder that cannot be written, then the exit code is still 0 and the reason is on stderr.
4. Given `--hook`, then Claude Code is started with settings whose `UserPromptSubmit` hook runs `filum-mcp log` on the run's memory folder.

## Open questions

None.

## Result (2026-10-06)

LongMemEval `oracle`, the same 20 questions, Claude Code with Haiku 4.5 through `filum-mcp`, with a consolidation session after each history session:

| | Without the hook | With the hook |
|---|---|---|
| Accuracy | 9 of 19 (47%; the 20th was not played) | **14 of 20 (70%)** |
| Extraction | 2 of 4 | 4 of 4 |
| Multi-session | 1 of 4 | 3 of 4 |
| Temporal | 1 of 3 | 3 of 4 |
| Knowledge updates | 1 of 4 | 0 of 4 |
| Abstention | 4 of 4 | 4 of 4 |

- Without the hook, 8 of the 10 failures were "no record": the model had not kept what was said.
- With the hook, everything said is in the log. What remains is knowledge updates, where the model answers from a file that was overwritten (spec 037).
- 96 agent sessions per run, on the owner's subscription; the judge cost under $0.01.
- The benchmark is repeated after the owner's refactoring.
