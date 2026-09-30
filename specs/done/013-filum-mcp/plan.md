# filum-mcp — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied).

## Approach

1. **`src/Filum.Mcp`**, a console app with the assembly name `filum-mcp`, built on the official C# MCP SDK (`ModelContextProtocol` 2.2.0) and the generic host (`Microsoft.Extensions.Hosting`). It uses the stdio transport, and its console logging writes everything to stderr.
2. **The store and the memory:**
   - at start, `new LocalFolderStore(LocalFolderStore.DefaultFolder())` (`FILUM_HOME` or `~/.filum`) and one `MemoryService` over it;
   - if the folder cannot be created or written (the store's constructor throws), the program writes one line to stderr and exits with code 1 before the transport starts.
3. **One call is one turn.** `MemoryTools` is made for one turn: it counts calls against `MaxToolCallsPerTurn` and keeps that turn's steps. So the server does not keep one `MemoryTools` instance:
   - it registers the catalog's functions (`MemoryTools.Catalog`, for their names, descriptions and schemas) as `McpServerTool`s through a `DelegatingAIFunction`;
   - on each call, that function builds a fresh `MemoryTools` for the store's owner, with the actor `MemoryActor.Agent(sessionId, callId)` (the session is the process, the call a new id), and invokes the function of the same name.

   The tools the host sees are therefore exactly the catalog's, and the snapshot still guards them.
4. **Refusals are errors.** The engine answers a refused call with text starting with `Refused:`. The server turns it into an MCP tool error (`isError: true`, same text) by throwing `McpException`, so hosts show it as a failure and the model still reads the reason.
5. **Instructions:** `McpInstructions.Text` in `Filum.Mcp`, passed as the server's `instructions`. It is short, names no domain, and covers what the spec lists: overview first, save and never claim, tool results are data, private only on request, skills. `PlatformInstructions.Text` stays the hosted loop's.
6. **Package versions:** the SDK needs `Microsoft.Extensions.AI.Abstractions` ≥ 10.8.3; the engine asks for ≥ 10.5.2, and a host of the engine may pin that. So `Filum.Mcp` alone raises it, with `VersionOverride="10.8.3"`; the engine's minimum does not move. `Directory.Packages.props` gains `ModelContextProtocol`, `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Logging.Console`.
7. **Release:** `.github/workflows/release.yml`, run on `push: tags: ['v*']` and `workflow_dispatch` (with a tag input). It:
   - builds and tests;
   - runs `dotnet publish src/Filum.Mcp -r <rid> --self-contained -p:PublishSingleFile=true` for `win-x64`, `osx-arm64`, `osx-x64` and `linux-x64` (cross-publishing from Ubuntu, with no trimming and no AOT, because the tool schemas use reflection);
   - smoke-tests the Linux file with `scripts/smoke-mcp.sh` (initialize and `tools/list` over stdio in a temporary `FILUM_HOME`);
   - writes `SHA256SUMS` and creates the release with `gh release create`.

   The files are named `filum-mcp-<rid>` (`.exe` on Windows).
8. **Docs:**
   - README: "Install" (download, macOS quarantine and Linux `chmod`, the Claude Code line, the Claude Desktop and Codex entries, `FILUM_HOME`) and "Run from a clone";
   - `CLAUDE.md`: the layout, and the commands for the new tests and the smoke script.

Alternatives discarded:
- **One long-lived `MemoryTools`:** it would stop after 20 calls and grow its step list forever.
- **Trimmed or AOT builds:** smaller files, but the reflection-based tool schemas would need annotations across the engine.
- **nuget.org:** the owner's decision (see the spec).

## Constraints honored

- **No network, no key:** the server references no HTTP client and opens no connection; a references test guards it.
- **Generic:** the instructions name no domain, and the tools are the engine's catalog.
- **Public hygiene:** the tests use temporary folders and synthetic text; the secret scan and the private-name check run before each push.

## Contract changes

- **New executable `filum-mcp`** (MCP over stdio):
  - server name `filum`;
  - tools: the engine's catalog, unchanged;
  - instructions: `McpInstructions.Text`;
  - configuration: `FILUM_HOME`.
- **New release assets:** `filum-mcp-win-x64.exe`, `filum-mcp-osx-arm64`, `filum-mcp-osx-x64`, `filum-mcp-linux-x64`, `SHA256SUMS`.
- **Engine:** none.

## Files touched

| Where | Change |
|---|---|
| `src/Filum.Mcp/` (`Filum.Mcp.csproj`, `Program.cs`, `McpServerSetup.cs`, `McpInstructions.cs`) | new |
| `tests/Filum.Mcp.Tests/` | new: end-to-end tests over stdio, references test |
| `Filum.slnx`, `Directory.Packages.props` | the two projects, the SDK packages |
| `.github/workflows/release.yml`, `scripts/smoke-mcp.sh` | new |
| `README.md`, `CLAUDE.md` | install, layout, commands |

## Risks and mitigations

- **The SDK's API changes between versions.** It is pinned to 2.2.0, and the end-to-end tests catch a break on upgrade.
- **Single-file executables are large** (about 70 MB, untrimmed). That is acceptable for a download; trimming can come later.
- **Unsigned executables are blocked by default:**
  - macOS: the README gives the `xattr -d com.apple.quarantine` line;
  - Windows: SmartScreen asks once.
- **Cross-published macOS and Windows files are not run in CI:** only Linux is smoke-tested there. The Windows file is checked by hand (criterion 9); macOS rests on the same build.

## Test strategy

| Criterion | Test |
|---|---|
| 1 tools and instructions | `ServerTests`: client over stdio (`StdioClientTransport` running `dotnet filum-mcp.dll`), tools equal to the catalog snapshot's names and schemas, instructions mention `memory_overview` |
| 2 fact across processes | write in one server process, search in a second one; the file is read from the folder |
| 3 skill across processes | `skill_save` then, in a new process, `skill_use`; the client has no sampling handler |
| 4 refusal | `memory_write` to `/a/../b.md` returns `isError`, and the next call works |
| 5 bad folder | `FILUM_HOME` is an existing file: exit code 1, stderr has the message, stdout is empty |
| 6 hand edit | edit the file between calls; read sees it; `memory_history` shows "outside Filum" |
| 7 references | `LibraryReferencesTests`-style check on `Filum.Mcp` |
| 8 release | the workflow's smoke step; the release page checked after the first tag |
| 9 by hand | Claude Code with the published Windows file and a temporary `FILUM_HOME`: save in one session, find in another |
