# Filum

**A memory and skills engine for AI agents** (MIT). One person runs Filum on their own machine as an MCP server (`filum-mcp`) inside their agent (Claude Code, Claude Desktop, Codex, Cursor…): the host's model does the thinking, Filum keeps the memory in a local folder of plain files with an append-only history, and learns procedures (skills) from the person. No API keys, no service, no account. Hosts can also use it as a library: `services.AddFilumEngine(...)`.

**No model of its own:** the engine thinks with its host's model (`filum-mcp`) or with the one a standalone use plugs in (`samples/Filum.Standalone`). Products build their own platform on it elsewhere; nothing of that belongs here.

**Version 1.x.** The libraries follow semantic versioning; releases carry ready `filum-mcp` executables. The repository is private until its clean public release, but it is written as a public one (see below). Read `docs/VISION.md` before designing anything.

## Layout

How the projects fit together, the entry point and the journey of a tool call are in `ARCHITECTURE.md`. In short:

| Folder | Content |
|---|---|
| `docs/` | `VISION.md` (what Filum is for), `CODING-STANDARD.md` (the coding rules), `EXCEPTIONS.md` (approved deviations) |
| `src/` | `Filum.Engine.Abstractions` (ports and models), `Filum.Engine` (the memory, the tools, imports, compute, packages, consolidation's rules; `AddFilumEngine`, `FilumEngine.Create`), `Filum.Engine.Storage.LocalFolder`, `Filum.Engine.Storage.InMemory`, `Filum.Embeddings` (the optional local search model and the keep gate), `Filum.Mcp` (`filum-mcp`), `Filum.Evals` (the eval runner, the `mcp` growth test and the `longmemeval` benchmark: by hand only, never in CI or `dotnet test`) |
| `evals/scenarios/` | the synthetic scenarios |
| `samples/` | `Filum.Standalone`: the engine's tools given to any `IChatClient`, on a local folder, through the public API only |
| `tests/` | one `*.UnitTests` / `*.IntegrationTests` project per production project, `Filum.Engine.Testing` (`MemoryServiceContract`, the contract every store must pass), `Filum.Engine.ArchitectureTests`; none needs a network, a model or Docker |
| `packs/` | `example/`: a minimal, domain-free package |
| `scripts/` | `check-secrets.sh` (run by CI and before every push), `smoke-mcp.sh <command>` (the MCP handshake against a build) |
| `specs/` | feature specs (spec-driven development) |

## Verification commands

From the repository root (.NET 10 SDK, nothing else):

```sh
dotnet build Filum.slnx
dotnet test Filum.slnx
bash scripts/check-secrets.sh
```

Warnings are errors; the architecture tests enforce layers, the public surface and namespaces. The public API is tracked in each library's `PublicAPI.*.txt`; a breaking change raises the major version. The tool surface is checked against `tests/Filum.Engine.IntegrationTests/Snapshots/ToolCatalog.snapshot.json` (regenerate with `FILUM_UPDATE_SNAPSHOT=1 dotnet test Filum.slnx`, then review the diff). A folder written by v0.7.1 (`tests/Filum.Engine.Storage.LocalFolder.IntegrationTests/Fixtures/folder-v0.7`) must always be read and continued: never regenerate it for a refactoring. CI (`.github/workflows/ci.yml`) runs the three commands on every push to `main` and on every pull request. A release (`.github/workflows/release.yml`) runs on a pushed `v*` tag or by hand: it builds and tests, publishes one self-contained `filum-mcp` per system (win-x64, osx-arm64, osx-x64, linux-x64), smoke-tests the Linux one and attaches them with `SHA256SUMS`. Tag only when the owner asks.

A change is done only when the commands for the part it touched pass. Git is trunk-based: work on `main`, small commits, no other branches.

## Written as a public repository

- **No secrets:** no keys, no tokens, no connection strings, no `.env`.
- **No personal data:** examples and tests are synthetic ("a person", `example.invalid`), never a real name, email, address or memory.
- **No private references:** no host names, IPs, tailnet names, internal repositories, deploy details, or the hosted product's internals. When unsure, leave it out and ask.
- **No domain content:** nothing about finance, health, work, sport… in code, prompts, tool descriptions or tests. Verticals are packages that live with the products that own them.
- Contributions come under the Developer Certificate of Origin: every commit is signed off (`git commit -s`); see [CONTRIBUTING.md](CONTRIBUTING.md).

## Development cycle

The AI-native cycle runs in local Claude Code through the commands in `.claude/commands/`: `/spec` (idea or notes → spec draft), `/plan` (reviewed spec → plan and tasks), `/implement` (tasks → code and tests), `/fix-bug` (bug → failing test → fix), `/review-changes` (review of changes). Humans review the spec before `/plan` and decide what gets pushed.

## The product rule

**Generic in code, personal in data.** Filum is an engine from which a personal memory *emerges* for each person; it is never tailored to one person or one domain. Every capability is a generic building block (files with history, collections with schemas, procedures as data). Before adding anything, ask: *would this work for any person, in any domain, without new code?*

## Spec-driven development

Non-trivial changes (observable behavior, a contract, a tool) start in `specs/`: copy `specs/_template/`, write `spec.md` + `spec.it.md`, get human review on the document, then `plan.md` and `tasks.md`.

## Work in phases: understand → change → verify

Finish one phase before starting the next and say which one you are in. If a later phase contradicts an earlier conclusion, go back to that phase instead of patching forward.

1. **Understand** (read only): read the spec first; trace the real flow end to end; never hide a symptom behind a guard. Before changing shared code, find every caller.
2. **Change** (smallest thing that works): do only what was asked; reuse what exists; no abstraction nobody asked for.
3. **Verify** (before claiming anything): run the tests and read the output; a bug fix gets a test that fails before and passes after. Keep verified facts separate from what you could not check, and say plainly when something was not run. Behavior stays unchanged in a refactoring; a visible behavior change goes in a commit of its own, with its test.
