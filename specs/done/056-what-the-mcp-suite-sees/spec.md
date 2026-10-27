# What the mcp suite sees, skills on request and rules in a fresh chat

> Status: Implemented (2026-10-21; acceptance 6 not met, see tasks.md)
> Area: engine, MCP, evals
> Author/date: federico rogora (drafted by Claude), 2026-10-21

## Problem

Six scenarios of the `mcp` suite fail in every run on the isolated harness, before and after spec 055 (the run of
2026-10-20): `count-sum`, `rule-follow`, `skill-command-it`, `skill-create-use`, `skill-propose-it` and `skill-starter`.
Their answers and tool calls (that run, `with filum`, Haiku 4.5) show two kinds of cause.

**The suite cannot see a pass (harness).**
- **Every turn is a new `claude -p`.** Turns of the same chat are not continued. "Sì, salvala" after a proposal
  reaches a model that never saw the proposal (`skill-propose-it`, turn 4: "I don't have context").
- **Tool calls become steps by one rule:** a writing tool is "wrote", anything else is "read". So:
  - `collection_aggregate` and `compute_run` are never a computed step (`count-sum` turn 2 cannot pass);
  - `skill_use` is never "Used skill /name" (`skill-starter` used the skill and answered right, and still failed);
  - `skill_propose` never gives a proposal (`skill-propose-it` turn 3 cannot pass);
  - `fact_end` is not a write.
- **`rule-follow` checks the core file's Rules section.** In filum-mcp a rule is a fact of kind rule (spec 045); the
  model saved it there with `fact_record`, and the check failed.

**Filum does the wrong thing (product).**
- **A skill asked for by name is only proposed.** "Create a skill called log-note: …" led to `skill_propose` and "Would
  you like me to save this skill?", so nothing was saved. Later chats had no `/log-note`. In `skill-command-it` the
  answer said "Fatto!" with nothing saved: a false claim.
- **A fresh chat with no tool call never sees the rules.** Without hooks, "What is the capital of Portugal?" was
  answered with no tool call at all, so the rule "end every answer with 'Onward.'" never reached the model. The brief
  reaches it only through `memory_overview`, which a model skips when the question looks general.
- **Totals are added up by reading.** `count-sum` got 3 runs and 25.5 km right, from `memory_read`, not from
  `collection_aggregate`. On three rows that is right; on three hundred it is not.

## Goal

The `mcp` suite measures what it claims to measure. With that fixed, a skill asked for by name is saved and works by
name in a new chat, a rule reaches a fresh chat even when the model calls no tool, and counts and totals come from
computation.

## Non-goals

- No new tool, and no change to the files' formats.
- The hosted app's flow stays: there, a skill Filum suggests is shown as a proposal and saved only on yes. This spec
  changes only the case where the person asks for a skill themselves.
- No domain words in code, prompts or tests.
- The hooks path does not change: with hooks, the brief already carries the rules.

## Current behavior

- `ClaudeCodeAgentStrategy` (`src/Filum.Evals/Mcp/`) starts a new `claude -p` for every turn, whatever its chat.
- `AgentToolSteps.Of` turns each tool name into a "wrote" or "read" step, with the description "Called <tool>".
- The MCP instructions (`src/Filum.Mcp/McpInstructions.cs`) say about skills: "when they ask for the same kind of
  thing again, offer one, and save it with skill_save only if they agree". They say nothing about a skill the person
  asks for.
- The server's instructions are the same text for everyone; the rules reach the model only through
  `memory_overview`, the brief hook or the recall hook.

## Desired behavior

1. **Chats continue.** The turns of one chat run in one Claude Code session (`--resume` with that chat's session id);
   a new chat starts a new session.
2. **Steps as the host reports them.**
   - `collection_aggregate` and `compute_run` are computed steps.
   - `skill_use` is "Used skill /<name>", with the name it was called with.
   - `skill_propose` gives the turn its proposal, read from the tool's input.
   - `fact_end` is a write.
3. **A rule is found where filum-mcp keeps it.** A `core_contains` check passes when the text is in the core file or
   in a rule that holds in the facts file; `core_not_contains` looks in both too. The check's name and meaning ("the memory keeps this rule") stay.
4. **A skill asked for is saved.** The instructions say: when the person asks for a skill (by name, or "make a skill
   that …"), save it with `skill_save` and say it is saved; offer one with `skill_propose` only when the idea is
   Filum's. Never say a skill is saved before `skill_save` has returned.
5. **Rules in the server's instructions.** At start, filum-mcp adds the rules that hold today to its instructions:
   at most 800 characters and only what fits under the 4,096-character limit with everything else (about 300 with the
   full profile, so the instructions were tightened), newest first, none when the facts file is private. The rest stays
   in `memory_overview`. A fresh chat then has the rules even when the model calls no tool. A rule set during the
   session reaches the model through recall and the brief, as now.
6. **Counts and totals by computation.** The instructions say that a count, a sum or an average over rows comes from
   `collection_aggregate` (or `compute_run`), never from adding up what was read.

## Platform check

1. Generic: chats, steps, skills, rules and computation, for any person and any domain.
2. Nothing private: with a private facts file, no rule goes in the server's instructions. The instructions already reach the host's
   context, as the brief does; this only moves the rules earlier. The tests use synthetic people.

## Acceptance criteria

1. Given two turns of one chat and one of another, when the suite runs them, then the first two share a Claude Code
   session and the third does not. Unit test on the arguments.
2. Given tool calls of `collection_aggregate`, `compute_run`, `skill_use`, `skill_propose` and `fact_end`, then their
   steps are computed, "Used skill /<name>", a proposal and a write. Unit tests.
3. Given a rule held as a fact, then `core_contains` with its words passes. Unit test.
4. Given a home with a rule, when filum-mcp starts, then its instructions hold it; with the facts file private, they
   do not; and the full profile's instructions stay under 4,096 characters with many rules. Tests.
5. Given the instructions, then they say a skill asked for is saved with `skill_save`, and totals come from
   computation. Unit test.
6. Given the `mcp` suite, isolated, Haiku 4.5, 3 repetitions, `with filum` only:
   - it runs once with the harness fixes (1-3) on the engine of today, and once with everything;
   - after everything, each of the six scenarios passes in at least 2 of 3 repetitions;
   - the other eleven scenarios lose no more than 1 pass in all, and false claims do not rise;
   - both runs are reported. About 4 $ eq. of quota in all; no real money.

## Decisions (were open questions)

1. **Rules in the instructions (item 5).** They reach every chat, also one about something else. The alternative is
   to leave them in `memory_overview` only, and accept that a model which calls no tool does not see them. Decided:
   in the instructions, since the person set them for every answer.
2. **The hosted app.** Should the hosted agent also save a skill asked for by name at once, instead of proposing it?
   Decided: not in this spec. The hosted flow stays (the code freeze); it would be a platform change of its own.
