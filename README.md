# Filum

**A memory and skills engine for AI agents.** Give any agent a memory it can read, a history it can audit, and
procedures it learns from you. Bring your own model.

> Status: pre-release (0.x). `filum-mcp` and the engine work today; the published evals and the packs come next, and
> names and formats may still change.

## What it does

- **Nothing said is lost.** Everything the person says is kept in an append-only log of events, with its date, and
  searchable. The memory the agent builds on it cites where each piece came from.
- **Memory as files with revisions.** Every fact lives in a plain file you can open. Every change is an append-only
  revision, so you can always see what the agent knew, when, and why it changed.
- **Facts that change over time.** "Three plants", then "four": the old value is closed with its date, not
  overwritten, so the memory knows what holds now and what held before.
- **Written live, tidied later.** The agent writes what matters while it answers, so it is known from the next
  message; a consolidation pass later catches what it missed, proposes new structures instead of creating them, and
  never overwrites what the person edited.
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
- **Your memory is a folder.** Plain files you can open and edit, plus an append-only log of every change and of
  what was said (`memory_log`), in `~/.filum` (or wherever `FILUM_HOME` points, or one folder per project). Your
  agent tidies it when Filum asks (`memory_consolidate`).
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

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download). The engine's and `filum-mcp`'s tests need nothing
else; the turn's tests (`tests/Filum.Agent.Tests`) run PostgreSQL in a container, so they need Docker:

```sh
dotnet build Filum.slnx
dotnet test Filum.slnx
```

| Folder | Content |
|---|---|
| `src/Filum.Engine/` | the engine: memory as files with revisions, collections, skills, the tool catalog, the local folder store |
| `src/Filum.Mcp/` | `filum-mcp`, the engine as an MCP server on stdio |
| `src/Filum.Agent/` | the turn, for a product that hosts the engine: instructions, the agent loop, the reliability check, models and providers, conversations, usage, the PostgreSQL store |
| `src/Filum.Agent.Http/` | what an ASP.NET host needs: the services from configuration and the endpoint groups |
| `samples/Filum.SampleHost/` | a sample host: its own database, one tool of its own, the example package |
| `tests/Filum.Engine.Testing/` | the contract every store must pass |
| `tests/Filum.Engine.Tests/` | the engine's tests: the contract in memory and on a local folder, the tool catalog snapshot |
| `tests/Filum.Mcp.Tests/` | `filum-mcp` end to end: the real server over stdio, on a temporary folder |
| `tests/Filum.Agent.Tests/` | the turn and its hosting, through the sample host over HTTP (Docker) |
| `src/Filum.Evals/`, `evals/scenarios/` | the eval runner and its synthetic scenarios (run by hand, never in CI) |
| `tests/Filum.Evals.Tests/` | the runner's logic: every check, the runner against the sample host, the growth test with a fake agent (Docker) |

Run the server from a clone with `dotnet run --project src/Filum.Mcp` (for example as the command an agent starts).
`bash scripts/smoke-mcp.sh <command>` checks that a build answers the MCP handshake.

The tool surface is checked against `tests/Filum.Engine.Tests/ToolCatalog.snapshot.json`. After a deliberate change,
regenerate it with `FILUM_UPDATE_SNAPSHOT=1 dotnet test Filum.slnx` and review the diff.

## Packages

A package makes the engine vertical with data, no code: the rules a product wants its agent to follow, the skills a
new memory starts with, and its first files and collections. Point `FILUM_PACK` at the package's folder in the server's
configuration (`claude mcp add filum -e FILUM_PACK=/path/to/pack -- ...`).

```
pack.json        {"format": 1, "name": "journal", "version": "1.0.0", "description": "One line."}
                 optional "private" / "sensitive": lists of memory/ files created with that sensitivity
prompt.md        the package's rules: they come before the person's core, which adds to them and never overrides them
skills/*.md      starter skills, in the skill file format (a package skill replaces a built-in one of the same name)
memory/**        the files a new memory starts with: documents (.md), collections (.csv with their header),
                 and filum.md to replace the core template
```

- Files and skills are given to a memory when it is created; the prompt is read at every session, so a new version
  of it reaches everyone.
- The whole package is checked with the engine's own rules at start. A package with any problem is refused with the
  list of problems, and the server does not start.
- [`packs/example/`](packs/example) is a minimal one: a journal, and a weekly look back.

## Host the engine in your service

A product can run the engine and the turn inside its own ASP.NET service, with its own login, database and tools
(`samples/Filum.SampleHost` does all of it):

```csharp
builder.Services.AddDbContextFactory<MyDbContext>(o => o.UseNpgsql(connectionString), ServiceLifetime.Scoped);
builder.Services.AddFilumAgent<MyDbContext>(builder.Configuration);   // models, providers, memory, package, checks
builder.Services.AddScoped<ITurnToolSource, MyTools>();              // your tools, beside the engine's
var app = builder.Build();
HostTools.CheckNames(app.Services);                                   // a clash with an engine tool stops here

app.MapGroup("/api")
    .RequirePerson(http => /* the person, from your own login */)
    .MapFilumConversations()
    .MapFilumUsage()
    .MapFilumMemory()
    .MapFilumSkills();
```

- Your context calls `FilumModel.Configure(modelBuilder)`; you keep the migrations.
- A tool made with `HostTools.Create` can return a `SurfacedResult(text, data)`: the model reads the text, and your
  app gets `data` (at most 4 KB) on the turn's step, even when the conversation is read again.
- `ITurnGate` can refuse a turn before the model, with your status and message; `ITurnObserver` hears the turns
  that answered, with their usage.
- `Agent:Name` names the assistant, `Agent:AllowModelChoice` keeps the choice of model to you, and `Agent:TimeZone`
  is given to your tools.
- Every message is recorded in the person's log of events (`memory_events`: add it to your migrations).
  `Consolidation:Enabled` runs a pass in the background after 10 minutes of quiet and nightly, with the default model;
  `POST …/memory/consolidate` runs one now, and `POST …/memory/forget` forgets a conversation and what came only from
  it.
- The groups are a versioned contract (v1, header `Filum-Api-Version`), described in
  [`docs/api/openapi-v1.json`](docs/api/openapi-v1.json) for generating your app's client; [`docs/api`](docs/api/README.md)
  says what v1 guarantees.

## Evals

Measurements, not claims. `src/Filum.Evals` plays scenarios (JSON: a few messages, and what must be true after each)
and scores each model on tasks done, changes claimed but not made, rules kept, cost and speed. It calls real models,
so it runs by hand only, never in CI; the judge is `gpt-5.4-mini` with `OPENAI_API_KEY`.

**Against a service that hosts the engine:**

```sh
dotnet run --project src/Filum.Evals -- --service http://localhost:5410 --prefix /api --register /api/auth/register \
  --models <ids, or host for the host's own model> --scenarios-dir <your folder> --reps 3 --cap 1
```

- `--person-header <name>` instead of `--register` for hosts that identify the person with a header.
- `--setup "<command>"` runs a command of your own after each new person is made, with `{email}` and `{person}`
  replaced, for example to give the account a plan.
- Besides memory checks, scenarios can check guardrails:
  - `tool` / `no_tool`;
  - `surfaced`, the card a tool returned, with `fields`, `equals` and `contain`;
  - `refused`, the turn gate's status, with nothing saved;
  - `host_unchanged`, a path of yours read before and after the turn;
  - `judge`, your own yes/no question about the answer.

**The growth test of `filum-mcp`:**

```sh
dotnet run --project src/Filum.Evals -- mcp --filum-mcp <filum-mcp executable or filum-mcp.dll> --max-sessions 8 --agent-model haiku
```

- Every turn is a new session of Claude Code (`claude -p`), in two arms: with only `filum-mcp`, and with no MCP server.
- Its own file and memory tools are off in both, so only Filum can remember.
- `--max-sessions` caps the run; the sessions use your own Claude subscription.

**LongMemEval** ([Wu et al.](https://github.com/xiaowu0162/LongMemEval), MIT), the benchmark that measures the memory:

```sh
bash scripts/longmemeval-download.sh oracle s           # into evals/longmemeval/data/, never committed
dotnet run --project src/Filum.Evals -- longmemeval --data evals/longmemeval/data/longmemeval_oracle.json \
  --subset evals/longmemeval/subset-oracle-20.json --host filum --service <url> --prefix <groups> --person-header <name> \
  --baselines none,naive --models <ids> --models-file <a host's appsettings.json> --cap 2
```

- **The protocol:** each history session is a new chat in date order, and the question comes last, in a new chat with its date. Answers are judged with the authors' prompts.
- **The systems:**
  - `--host <label>`: Filum on a host configured for that label, for example with `Reliability:CheckModel` empty for the ablation without the claim check; `--consolidate` runs a consolidation pass after each session;
  - `--baselines none,naive`: no memory, or BM25 over the sessions;
  - `--claude filum,none,full`: Claude Code with `filum-mcp`, with no memory, or with the whole history in the prompt.

## Roadmap

The engine library, the local folder memory, `filum-mcp`, packages, hosting and evals for any host, stable endpoints for apps, the LongMemEval benchmark and the memory core with its log of events (done); next, ingestion from files and connectors, faster gating and hybrid retrieval. The detailed
specs are published alongside the code, in `specs/`.

## License

MIT. Contributions require signing the [CLA](CLA.md).
