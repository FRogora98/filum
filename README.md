# Filum

**A memory and skills engine for AI agents.** Give any agent a memory it can read, a history it can audit, and
procedures it learns from you. Bring your own model.

> Status: pre-release. The engine library and its local folder memory are here; `filum-mcp`, the part you install,
> comes next. Watch the repo or come back in a few weeks.

## What it does

- **Memory as files with revisions.** Every fact lives in a plain file you can open. Every change is an append-only
  revision, so you can always see what the agent knew, when, and why it changed.
- **Skills: procedures, not just facts.** When you repeat a task, the agent proposes to save it as a skill. Next time,
  one call runs it on fresh data.
- **Typed collections.** Lists with a schema (people, decisions, anything), kept next to the free-form memory.
- **Works with small models.** Reliability is measured with evals against cheap models and the results are published.
- **Domain-free.** The engine knows nothing about your domain. Verticals are *packs* (a prompt, preinstalled skills,
  collection schemas) that live with the products that own them.

## How you use it

Run `filum-mcp` inside Claude Code, Claude Desktop, Codex or any MCP host, with one line of configuration. It is for
one person, on their own machine:

- **No API keys, no service, no account.** The host's model does the thinking; Filum gives it the tools.
- **Your memory is a folder.** Plain files you can open and edit, plus an append-only log of every change, in
  `~/.filum` (or wherever `FILUM_HOME` points, or one folder per project).
- **Nothing leaves your machine.**

## Build and test

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). No Docker, no database, no network:

```sh
dotnet build Filum.slnx
dotnet test Filum.slnx
```

| Folder | Content |
|---|---|
| `src/Filum.Engine/` | the engine: memory as files with revisions, collections, skills, the tool catalog, the local folder store |
| `tests/Filum.Engine.Testing/` | the contract every store must pass |
| `tests/Filum.Engine.Tests/` | the engine's tests: the contract in memory and on a local folder, the tool catalog snapshot |

The tool surface is checked against `tests/Filum.Engine.Tests/ToolCatalog.snapshot.json`. After a deliberate change,
regenerate it with `FILUM_UPDATE_SNAPSHOT=1 dotnet test Filum.slnx` and review the diff.

## Roadmap

The engine library and the local folder memory (done), then `filum-mcp` and the published evals. The detailed specs are
published alongside the code, in `specs/`.

## License

MIT. Contributions require signing the [CLA](CLA.md).
