# A clean agent interface

> Status: Implemented (2026-10-18). Reviewed (the owner, 2026-10-18, gap 5 of the engine audit; the fact policy and the default profiles decided the same day)
> Area: engine (tools, instructions), filum-mcp
> Author/date: federico rogora (drafted by Claude), 2026-10-18

## Problem

The independent audit found the agent interface behind current practice:
- **Errors are a string prefix.** A refused call returns `"Refused: …"` (`MemoryToolGroup.Run`), and filum-mcp recognises the
  prefix and throws (`PerCallFunction`), which a host sees as a protocol error rather than a tool result the model can
  read and act on.
- **No annotations.** A host cannot tell `memory_delete` from `facts_current`: no `readOnlyHint`, `destructiveHint` or
  `idempotentHint`.
- **No structured output.** Facts and search hits come back as prose; a host or an evaluation cannot parse them.
- **Two fact policies.** filum-mcp's instructions say to record every fact the person states with `fact_record`; the
  platform's say facts about the person go in the core or documents, `fact_record` only for values that change. The
  instructions live in separate texts that drifted.
- **32 tools at once.** Well above current advice for a model's tool list; there is no smaller profile.

## Goal

A host gets typed results, annotations and structured data; one fact policy reaches every model from one source; a
new filum-mcp user starts with a small set of tools.

## Non-goals

- No new tool and no change to what any tool does to the memory.
- No change to the text a model reads for a successful call.

## Desired behavior

1. **Refusals are typed.** The tool session records whether the last call was refused. filum-mcp returns a refused call
   as a tool result with `isError: true` and the reason as its text, not as a protocol error. In-process hosts see the
   same text as before.
2. **Annotations.** Every engine tool carries `readOnlyHint`, `destructiveHint` and `idempotentHint` (and
   `openWorldHint: false`), from one table in the engine (`ToolTraits`), so every host reads the same values. Read tools
   (`memory_overview`, `memory_read`, `memory_search`, `events_search`, `events_read`, `facts_current`, `facts_history`,
   `memory_history`, `skill_list`, `collection_aggregate`, `collection_trend`, `compute_run`) are read-only; deletions
   and undo are destructive; the others write without destroying.
3. **Structured output** for `facts_current`, `facts_history` and `events_search`: alongside the text, filum-mcp returns
   `structuredContent` (the facts with subject, attribute, value, from, to; the matches with id, date, who and text)
   and declares its `outputSchema`.
4. **One fact policy, one source.** The shared parts of the instructions (how to keep what the person says, the fact
   policy, how to search what was said) are written once in the engine (`InstructionParts`) and composed into both
   `PlatformInstructions.Text` and filum-mcp's instructions. The policy: `fact_record` for every point fact that can
   change, about a person, a place or a thing (where someone lives, a job, how many, a status, a preference), and the
   core and documents for who the person is and for longer texts (notes, plans). A test checks that both surfaces carry
   the same policy text.
5. **Profiles.** `core` (13 tools: `memory_overview`, `memory_read`, `memory_search`, `events_search`, `events_read`,
   `memory_write`, `memory_edit`, `memory_append`, `memory_delete`, `fact_record`, `facts_current`, `facts_history`,
   and filum-mcp's `memory_log`) and `full` (every tool). filum-mcp defaults to `core`, set with `FILUM_TOOLS=full` or
   its configuration; the engine's library default stays `full`. Instructions mention only tools in the profile.
6. **Changelog.** The new policy text (`PlatformInstructions.Text` is a public constant: a minor change) and filum-mcp's
   new default profile are written in the changelog, with how to turn `full` back on.

## Platform check

1. Generic: interface and policy, no domain.
2. Sensitivity: unchanged; structured output carries what the text already carried, private content only where the
   text would.

## Acceptance criteria

1. Given a refused call through filum-mcp, then the result has `isError: true` and the reason as text, and no protocol
   error is raised.
2. Given filum-mcp's tool list, then each tool has the annotations of `ToolTraits`; `memory_delete` is destructive,
   `facts_current` read-only.
3. Given facts recorded, when `facts_current` is called through filum-mcp, then `structuredContent` holds them and the
   tool lists an `outputSchema`; the same for `facts_history` and `events_search`.
4. A test reads the fact policy from both `PlatformInstructions.Text` and filum-mcp's instructions and finds the same text.
5. Given filum-mcp with no setting, then it lists the 13 core tools; with `FILUM_TOOLS=full`, every tool.
6. A regression check on the quota, pre-registered: 50 of 5c's LongMemEval questions through filum-mcp (core profile, new
   policy) against filum-mcp at fa75076; it must not lose more than 5 percentage points. A control, not a claim.
