# Filum

**A memory and skills engine for AI agents**, open source (MIT). One person runs Filum on their own machine as an MCP server (`filum-mcp`) inside their agent (Claude Code, Claude Desktop, Codex, Cursor…): the host's model does the thinking, Filum keeps the memory in a local folder of plain files with an append-only history, and learns procedures (skills) from the person. No API keys, no service, no account.

The engine is also used, as a library, by a hosted product that lives elsewhere; nothing of that product belongs here.

**Pre-release (0.x).** The engine, its local folder store, packages, `filum-mcp` and the libraries to host the turn in a product are here; releases carry ready executables. Read `docs/VISION.md` before designing anything.

## Layout

| Folder | Content |
|---|---|
| `docs/` | **what Filum is for** (functional, not implementation): `VISION.md` (the problem, the thesis, the principles, what Filum is not). `api/`: the endpoint groups' contract v1 (`openapi-v1.json`, a snapshot checked by `OpenApiTests`; `README.md`, what v1 guarantees) |
| `src/` | the code: `Filum.Engine` (memory, collections, skills, the tool catalog, the local folder store `LocalFolderStore`, packages `Pack`; no web, no database, no model); `Filum.Mcp` (`filum-mcp`: the catalog as MCP tools over stdio, one fresh `MemoryTools` per call, refusals as tool errors, `McpInstructions` for the host, logs on stderr); `Filum.Agent` (the turn: instructions, agent loop, reliability check, models and providers, conversations, usage, the Postgres store, and the hosting points of spec 017: `AgentOptions`, `ITurnToolSource`, `SurfacedResult`, `ITurnGate`, `ITurnObserver`, `HostTools`; no ASP.NET); `Filum.Agent.Http` (`AddFilumAgent`, `RequirePerson`, the endpoint groups) |
| `src/Filum.Evals`, `evals/scenarios/` | the eval runner (`filum-evals`: any host with `--prefix`, `--register` or `--person-header`, `--setup`, `--models host`; the `mcp` growth test with `claude -p`) and the 17 synthetic scenarios. By hand only: it calls paid models or the owner's agent, never in CI or `dotnet test` |
| `samples/` | `Filum.SampleHost`: a domain-free host of the engine (its own context, a header as a sample-only person, `sample_now`, the example package, the groups under `/sample`) |
| `tests/` | `Filum.Engine.Testing` (`MemoryServiceContract`, the contract every store must pass) and `Filum.Engine.Tests` (the contract in memory and on a local folder, the store, the tool catalog) and `Filum.Mcp.Tests` (the real server over stdio on a temporary `FILUM_HOME`), with no network and no Docker; `Filum.Agent.Tests` (the sample host over HTTP on PostgreSQL in a container: Docker needed); `Filum.Evals.Tests` (every expectation, the runner against the sample host with a fake model, the growth test with a fake agent; Docker needed) |
| `packs/` | `example/`: a minimal, domain-free package (spec 016); real packages live with the products that own them |
| `scripts/` | `check-secrets.sh`: fails when a tracked file looks like it holds a secret; run by CI and before every push. `smoke-mcp.sh <command>`: the MCP handshake against a build |
| `specs/` | feature specs (spec-driven development) |

## Verification commands

From the repository root (.NET 10 SDK; Docker running for `tests/Filum.Agent.Tests` and `tests/Filum.Evals.Tests`, the rest needs nothing else):

```sh
dotnet build Filum.slnx
dotnet test Filum.slnx
bash scripts/check-secrets.sh
```

The tool surface is `MemoryTools.Catalog`, checked against `tests/Filum.Engine.Tests/ToolCatalog.snapshot.json`, and the endpoint groups' contract against `docs/api/openapi-v1.json` (regenerate both with `FILUM_UPDATE_SNAPSHOT=1 dotnet test Filum.slnx`, then review the diff; within v1 only additions, see `docs/api/README.md`). CI (`.github/workflows/ci.yml`) runs the three commands by hand and on every pull request. A release (`.github/workflows/release.yml`) runs on a pushed `v*` tag or by hand: it builds and tests, publishes one self-contained `filum-mcp` per system (win-x64, osx-arm64, osx-x64, linux-x64), smoke-tests the Linux one and attaches them with `SHA256SUMS`. A release is public: tag only when the owner asks.

A change is done only when the commands for the part it touched pass. Git is trunk-based: work on `main`, small commits, no other branches.

## This repository is public

- **No secrets:** no keys, no tokens, no connection strings, no `.env`.
- **No personal data:** examples and tests are synthetic ("a person", `example.invalid`), never a real name, email, address or memory.
- **No private references:** no host names, IPs, tailnet names, internal repositories, deploy details, or the hosted product's internals. When unsure, leave it out and ask.
- **No domain content:** nothing about finance, health, work, sport… in code, prompts, tool descriptions or tests. Verticals are packages that live with the products that own them.
- Contributions require the [CLA](CLA.md).

## Development cycle

The AI-native cycle runs in local Claude Code through the commands in `.claude/commands/`: `/spec` (idea or notes → spec draft), `/plan` (reviewed spec → plan and tasks), `/implement` (tasks → code and tests), `/fix-bug` (bug → failing test → fix), `/review-changes` (review of changes). Humans review the spec before `/plan` and decide what gets pushed: pushing here publishes.

## The product rule

**Generic in code, personal in data.** Filum is an engine from which a personal memory *emerges* for each person; it is never tailored to one person or one domain. Every capability is a generic building block (files with history, collections with schemas, procedures as data). Before adding anything, ask: *would this work for any person, in any domain, without new code?*

## Spec-driven development

Non-trivial changes (observable behavior, a contract, a tool) start in `specs/`: copy `specs/_template/`, write `spec.md` + `spec.it.md`, get human review on the document, then `plan.md` and `tasks.md`.

## Work in phases: understand → change → verify

Finish one phase before starting the next and say which one you are in. If a later phase contradicts an earlier conclusion, go back to that phase instead of patching forward.

1. **Understand** (read only): read the spec first; trace the real flow end to end; never hide a symptom behind a guard. Before changing shared code, find every caller.
2. **Change** (smallest thing that works): do only what was asked; reuse what exists; no abstraction nobody asked for.
3. **Verify** (before claiming anything): run the tests and read the output; a bug fix gets a test that fails before and passes after. Keep verified facts separate from what you could not check, and say plainly when something was not run.
