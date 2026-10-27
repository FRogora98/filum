# Plan: capture and adherence without the host's nudges (spec 055)

## Contracts first

- **Facts.**
  - `FactService.CurrentAsync` without `on` means held today, by the engine's `TimeProvider`.
  - A new internal `FactService.PlannedAsync` gives the values that start after today.
  - No public API changes: `IMemoryFacts.CurrentFactsAsync` keeps its signature, and its "now" means today.
- **Today's override.**
  - filum-mcp reads `FILUM_TODAY` (`yyyy-MM-dd`) and gives the engine and the local store a clock on that day. It is
    used by the server and by the three hooks.
  - An invalid value is ignored with a line on stderr: a hook must never stop the host.
  - `evals/bench` sets the variable to each session's day and to the probe day.
- **Ending provenance.**
  - `fact_end` takes optional `sources` (event ids), kept in the ended row's sources.
  - The tool catalog snapshot changes, and is reviewed.
- **Text.**
  - New `InstructionParts`: `RuleReach` (style and format only), `Unknown` (abstention) and `WithoutAsking`
    (promises, closures, asked-for lists).
  - They are composed into the MCP instructions, `ConsolidationRules` and `ConsolidationPrompt`.

## Tasks

See `tasks.md`.
