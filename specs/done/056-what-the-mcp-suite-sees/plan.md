# Plan: what the mcp suite sees, skills on request and rules in a fresh chat

Spec: [spec.md](spec.md). Contracts first, then the harness, then the product, then the measure.

## Contracts

- **Engine, additive (2.x):** `IMemoryBriefing.RulesAsync(Guid userId, int? maxChars, CancellationToken)` and the same on
  `IMemoryService`: the "Rules the person set" section of the brief alone, newest first, within `maxChars`; empty with no
  rule or a private facts file. `PublicAPI.Unshipped.txt` gains both.
- **Evals, internal:** `IAgentStrategy.AskAsync` takes the chat's session (`AgentChat`: an id and whether it already
  started). `AgentSession.Tools` becomes a list of `AgentToolCall(Name, Input)`.
- **Tool surface:** unchanged (no snapshot change).

## Harness (`src/Filum.Evals/Mcp`, `Checks`)

1. `McpScenarioRunnerStrategy` keeps one `AgentChat` and one working folder per chat of a scenario; the first turn of a
   chat passes `--session-id <id>`, the next ones `--resume <id>`. The isolated config folder already lasts the whole
   run, so Claude Code finds the session again.
2. `AgentOutput` keeps each tool call's input. `AgentToolSteps` maps `collection_aggregate` and `compute_run` to
   `computed`, `skill_use` to a read "Used skill /<name>", `fact_end` to `wrote`; `skill_propose` gives the turn its
   `SkillProposalDto` from the input.
3. `FilumMemoryReader` adds the rules that hold (through `RulesAsync`) to the snapshot; `core_contains` looks in the core
   and in them.

## Product

4. `McpInstructions`: the skill line says a skill the person asks for is saved with `skill_save` at once, and
   `skill_propose` is only for Filum's own idea; a line (or `InstructionParts`) says counts and totals over rows come from
   `collection_aggregate` or `compute_run`. The full profile stays under 4,096 characters with the rules' share.
5. `McpStartup.OpenAsync`, after the memory is open, reads `RulesAsync(owner, 800)` once and keeps it on `OpenedMemory`;
   `McpServerConfiguration` appends it to the instructions.

## Measure

6. The `mcp` suite, isolated, Haiku 4.5, 3 repetitions, `with filum` only: on the engine with 1-3 only, then with all.
   Reported in tasks.md.
