# Lean turns — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (delegated by the owner).

## Approach

Three small changes, measured together:

1. **Two rules in `PlatformInstructions.Text`:**
   - a value that changed by an amount is worked out and recorded;
   - a question about what holds now, before, since when or how many is answered from the facts and checked with `events_search`, never from the first thing found.
2. **Every tool's description and its parameters' descriptions rewritten short:** what the tool does, nothing the instructions already say. Names, parameters, defaults and behavior stay as they are. A test bounds the catalog at 10,000 characters as the model receives it.
3. **The hosted turn leaves `memory_overview` out of the tools it offers:** its content (core, map, skills) is already in the instructions. `filum-mcp` keeps it, since its host has no such instructions.

**Discarded:**
- **Tools by need** (offering skill or history tools only on some messages): it needs a rule to pick them, which is either a model call or a guess on the message's words. The catalog's size is bounded instead.
- **Prompt caching:** these models' providers through OpenRouter report no cached tokens (probe of 2026-10-02). A host that pins a provider with caching gets it without engine changes.

## Constraints honored

- Generic: the rules speak of values that change, in any domain.
- No behavior of a tool changes, and sensitivity rules are untouched.

## Contract changes

- **Tool descriptions:** the snapshot changes in descriptions only; names, parameters and schemas do not.
- **The hosted turn's tools:** no `memory_overview`. The catalog (`MemoryTools.Catalog`) and `filum-mcp` still have it.
- **Configuration, endpoints:** none.

## Files touched

| File | Change |
|---|---|
| `src/Filum.Engine/MemoryTools.cs` | shorter descriptions |
| `src/Filum.Engine/PlatformInstructions.cs` | the two rules |
| `src/Filum.Agent/ConversationService.cs` | no `memory_overview` in a turn |
| `tests/Filum.Engine.Tests/ToolCatalogTests.cs`, `ToolCatalog.snapshot.json` | the size bound; the snapshot |
| `tests/Filum.Agent.Tests/LeanTurnTests.cs` | criteria 2 and 3 (instructions) |

## Risks and mitigations

- **Shorter descriptions could make small models misuse tools.** The engine's eval scenarios run on the default model before closing (criterion 3); a regression brings the longer text back for that tool.
- **Rollback:** one commit.

## Test strategy

| Criterion | Test |
|---|---|
| 1 | `ToolCatalogTests` (size bound) and the snapshot |
| 2 | `LeanTurnTests` |
| 3 | `LeanTurnTests` (the rules); the eval scenarios on the default model, by hand |
| 4 | LongMemEval `oracle` on Gemma, by hand |
