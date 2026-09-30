# Filum

**A memory and skills engine for AI agents.** Give any agent a memory it can read, a history it can audit, and
procedures it learns from you. Bring your own model.

> Status: pre-release (0.x). `filum-mcp` and the engine work today; the published evals and the packs come next, and
> names and formats may still change.

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

## Install

1. Download `filum-mcp` for your system from the [latest release](https://github.com/FRogora98/filum/releases/latest):
   `filum-mcp-win-x64.exe`, `filum-mcp-osx-arm64` (Apple silicon), `filum-mcp-osx-x64` (Intel Mac) or
   `filum-mcp-linux-x64`. It is one file with nothing else to install. Put it anywhere, for example `~/bin`.
   - macOS and Linux: `chmod +x filum-mcp-*`. On macOS the file is not signed yet, so also run
     `xattr -d com.apple.quarantine filum-mcp-osx-*` once.
   - Windows: SmartScreen may ask once; choose "More info", then "Run anyway".
   - The release's `SHA256SUMS` lets you check the download.
2. Register it with your agent.

**Claude Code** (one memory for all your projects):

```sh
claude mcp add filum --scope user -- /path/to/filum-mcp
```

**Claude Desktop**: in `claude_desktop_config.json` (Settings → Developer → Edit Config):

```json
{ "mcpServers": { "filum": { "command": "/path/to/filum-mcp" } } }
```

**Codex**: in `~/.codex/config.toml`:

```toml
[mcp_servers.filum]
command = "/path/to/filum-mcp"
```

On Windows, double the backslashes in JSON, for example `"C:\\Users\\me\\bin\\filum-mcp-win-x64.exe"`.

Start a new session and tell the agent something worth remembering. In the next session, ask about it.

**Where the memory is:** `~/.filum` by default, created on first use. To keep a separate memory, for example one per
project, set `FILUM_HOME` in the server's configuration: `claude mcp add filum -e FILUM_HOME=/path/to/folder -- ...`,
`"env": { "FILUM_HOME": "..." }` in JSON, or `env = { FILUM_HOME = "..." }` in TOML. The files are yours: open
them, edit them, back them up. Filum notices hand edits and records them in the history.

## Build and test

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). No Docker, no database, no network:

```sh
dotnet build Filum.slnx
dotnet test Filum.slnx
```

| Folder | Content |
|---|---|
| `src/Filum.Engine/` | the engine: memory as files with revisions, collections, skills, the tool catalog, the local folder store |
| `src/Filum.Mcp/` | `filum-mcp`, the engine as an MCP server on stdio |
| `tests/Filum.Engine.Testing/` | the contract every store must pass |
| `tests/Filum.Engine.Tests/` | the engine's tests: the contract in memory and on a local folder, the tool catalog snapshot |
| `tests/Filum.Mcp.Tests/` | `filum-mcp` end to end: the real server over stdio, on a temporary folder |

Run the server from a clone with `dotnet run --project src/Filum.Mcp` (for example as the command an agent starts).
`bash scripts/smoke-mcp.sh <command>` checks that a build answers the MCP handshake.

The tool surface is checked against `tests/Filum.Engine.Tests/ToolCatalog.snapshot.json`. After a deliberate change,
regenerate it with `FILUM_UPDATE_SNAPSHOT=1 dotnet test Filum.slnx` and review the diff.

## Roadmap

The engine library, the local folder memory and `filum-mcp` (done), then packages and the published evals. The detailed specs are
published alongside the code, in `specs/`.

## License

MIT. Contributions require signing the [CLA](CLA.md).
