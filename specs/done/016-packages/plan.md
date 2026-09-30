# Packages — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied).

## Approach

1. **`Pack`** (`src/Filum.Engine/Pack.cs`), a record: `Name`, `Version`, `Prompt?`, `Core?`, `Skills` (`IReadOnlyList<Skill>`), `Files` (path, content, sensitivity).
   - `Pack.Load(string folder, MemoryOptions limits)` reads the folder and returns the pack, or throws `PackException` with every problem it found.
   - It first checks the shape:
     - `pack.json` present, valid JSON, `format` = 1, `name` valid, `version` and `description` present;
     - `private` and `sensitive` name files of `memory/`;
     - skills parse and are named after their file;
     - `memory/` holds no `/skills/` file, because skills go in `skills/`;
     - `prompt.md` is at most `MaxCoreChars`.
   - Then it does a **dry run**: it applies the pack to an empty `InMemoryMemoryStore` through a `MemoryService`, which collects every refusal. So paths, content (CSV header, core length, skill format), file sizes and the file count are checked by the engine's own rules, with nothing duplicated.
2. **`MemoryService` takes an optional `Pack? pack = null`** as its last constructor parameter (DI leaves it null when no pack is registered) and exposes it as `Pack`. `EnsureCoreAsync` then:
   - creates the core from `pack.Core ?? CoreTemplate`;
   - writes each pack file with the author "platform" and its sensitivity, in one revision each (operation `write`);
   - saves the starters, except those the pack replaces by name, then the pack's skills.

   Refusals during the real application are logged; the dry run makes them impossible in practice.
3. **The prompt:** `PlatformInstructions.PackSection(Pack)` returns `# The rules of this assistant (package "<name>"): the person's core adds to them and never overrides them`, followed by the prompt. It is used in three places:
   - `Compose(…, pack: …)`, with a new optional parameter, placed between `Text` and the core;
   - `memory_overview`, at the top of its answer;
   - `filum-mcp`, appended to `McpInstructions.Text` in the server instructions.

   The snapshot is unchanged: no tool description changes.
4. **`filum-mcp`:** `FILUM_PACK` is read at start; a `PackException` prints the problems to stderr and exits with code 1.
5. **The example package:** `packs/example/` holds:
   - a short journal prompt;
   - the skill `weekly-review`;
   - `memory/journal.csv` (`date,entry`, marked sensitive);
   - `memory/about-this-pack.md`.
6. **Docs:** a README section "Packages" (the format, `FILUM_PACK`, the example), and the layout in `CLAUDE.md`.
7. **Private repository, after the submodule update:** the service reads `Memory:PackPath`, loads the pack at start (failing fast) and registers it; `ConversationService` passes `memory.Pack` to `Compose`. It has one integration test.

Alternatives discarded:
- **A second copy of the validation rules** in the loader: they would drift from the engine's.
- **Copying the prompt into the core:** it would be overridable by the person and would not follow a new package version.

## Constraints honored

- **Generic:** the loader and the format know no domain; the example names none.
- **Tools unchanged:** the snapshot stays the same.
- **The person's files are theirs:** packs only create files when a memory is created, and their revisions can be undone.

## Contract changes

- **The package format 1** (spec).
- `FILUM_PACK` for `filum-mcp`.
- **Engine API:**
  - `Pack`, `PackException`;
  - `MemoryService(…, Pack? pack = null)` and `MemoryService.Pack`;
  - `PlatformInstructions.PackSection` and `Compose(…, Pack? pack = null)`.

## Files touched

| Where | Change |
|---|---|
| `src/Filum.Engine/Pack.cs` | new |
| `src/Filum.Engine/MemoryService.cs` | optional pack, `EnsureCoreAsync` applies it |
| `src/Filum.Engine/PlatformInstructions.cs` | `PackSection`, `Compose` parameter |
| `src/Filum.Engine/MemoryTools.cs` | overview starts with the pack section |
| `src/Filum.Mcp/Program.cs`, `McpServerSetup.cs` | `FILUM_PACK` |
| `packs/example/**` | new |
| `tests/Filum.Engine.Tests/PackTests.cs`, `tests/Filum.Mcp.Tests/ServerTests.cs` | new tests |
| `README.md`, `CLAUDE.md` | docs |

## Risks and mitigations

- **The dry run and the real application diverge**, for example with limits that differ. Both use the same `MemoryOptions` given to `Load`, and the host passes the same options.
- **A host forgets to pass the pack to `Compose`.** A private integration test covers the hosted loop.

## Test strategy

| Criterion | Test |
|---|---|
| 1 | `PackTests`: example pack on a new memory (in memory and on a local folder): core, files, sensitivity, starters and pack skills, platform revisions |
| 2 | existing tests unchanged, plus one explicit no-pack test |
| 3 | a pack with three faults: `PackException` lists three problems; the target store stays empty |
| 4 | `Compose` order; overview starts with the pack section; `filum-mcp` instructions contain the prompt |
| 5 | `Compose` output: pack section heading says the core never overrides it, and it comes before the core |
| 6 | a memory created without the pack, then a service with the pack: files unchanged, `Compose` has the prompt |
| 7 | `filum-mcp` with a broken `FILUM_PACK`: exit 1, problems on stderr |
| 8 | snapshot test unchanged |
