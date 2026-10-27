# Filum

**A memory and skills engine for AI agents.** Give any agent a memory it can read, a history it can audit, and
procedures it learns from you. Bring your own model.

> Status: 1.0.0, the first public release. `filum-mcp` and the libraries follow semantic versioning: a breaking change of their public API or of
> the memory folder's format raises the major version.

## What it does

- **Nothing said is lost.** Everything the person says is kept in an append-only log of events, with its date, and
  searchable. The memory the agent builds on it cites where each piece came from.
- **Memory as files with revisions.** Every fact lives in a plain file you can open. Every change is an append-only
  revision, so you can always see what the agent knew, when, and why it changed.
- **Your files come in too.** A WhatsApp chat export, a mailbox (`mbox`), a calendar (`ics`) or a CSV becomes events
  with their own dates, previewed before anything is written, never imported twice, and forgotten whole by its source.
- **Facts that change over time.** "Three plants", then "four": the old value is closed with its date, not
  overwritten, so the memory knows what holds now and what held before.
- **Written live, tidied later.** The agent writes what matters while it answers, so it is known from the next
  message; a consolidation pass later catches what it missed, proposes new structures instead of creating them, and
  never overwrites what the person edited.
- **It learns you, and shows what it learned.** What it notices without being told (your words for people and
  things, your habits, how you like answers) goes in `/learned.md`, apart from what you told it, which always wins.
- **Skills: procedures, not just facts.** When you repeat a task, the agent proposes to save it as a skill. Next time,
  one call runs it on fresh data.
- **Typed collections.** Lists with a schema (people, decisions, anything), kept next to the free-form memory.
- **Numbers computed, not guessed.** Totals come from code (`collection_aggregate`); where a value is heading and what
  stands out come from a forecast with its error on the past (`collection_trend`), always called an estimate.
- **Measured, small models included.** Which models and setups work, and which do not, is in [Results](#results-with-their-limits).
- **Domain-free.** The engine knows nothing about your domain. Verticals are *packs* (a prompt, preinstalled skills,
  collection schemas) that live with the products that own them.

## How you use it

Run `filum-mcp` inside Claude Code, Claude Desktop, Codex or any MCP host, with one line of configuration. It is for
one person, on their own machine:

- **No API keys, no service, no account.** The host's model does the thinking; Filum gives it the tools.
- **Your memory is a folder.** Plain files you can open and edit, plus an append-only log of every change and of
  what was said (`memory_log`), in `~/.filum` (or wherever `FILUM_HOME` points, or one folder per project). Your agent
  tidies it when Filum asks (`memory_consolidate`); with `FILUM_TOOLS=full` it also brings your files in
  (`memory_import`).
- **Search by meaning, locally.** On first start `filum-mcp` downloads a small multilingual model (about 120 MB, into
  `~/.filum/models`, or `FILUM_MODELS`) and finds what was said in other words too; until then, and with
  `FILUM_EMBEDDINGS=off`, it searches by words. A search of what was said shows the facts that may answer it, with the
  conversation they came from, and the best conversations as whole as a budget allows.
- **An optional reranker.** With `FILUM_RERANKER=on`, `filum-mcp` downloads bge-reranker-v2-m3 (Apache-2.0, about
  575 MB) and orders a search's best matches again with it, on your CPU (a few seconds a search). Off by default:
  nothing is downloaded unless you turn it on.
- **Nothing leaves your machine.**
- **Fourteen tools to start with.** `filum-mcp` offers a core set by default: reading and searching the files and what
  was said (`memory_overview`, `memory_read`, `memory_search`, `events_search`, `events_read`), writing files
  (`memory_write`, `memory_edit`, `memory_append`, `memory_delete`), the facts (`fact_record`, `fact_end`,
  `facts_current`), `memory_log`, and tidying (`memory_consolidate`), which catches later what a turn did not save. Set
  `FILUM_TOOLS=full` to offer every tool: a fact's whole history (`facts_history`), collections, computing, skills,
  history and undo, and imports (`memory_import`).
- **Now is today.** A value that starts on a later day is shown as planned, and the one it replaces still holds.
  To replay a history, set `FILUM_TODAY=yyyy-MM-dd`: `filum-mcp` and its hooks take that day as today.
- **Rules and open items.** A lasting instruction ("from now on…") and something promised or still to do are kept as
  facts (attributes `rule` and `open`). The rules come first every time a session starts, and again with every message.

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

**It knows you from the first message (Claude Code).** Add three hooks to `~/.claude/settings.json` (with `--home` when
the server has its own `FILUM_HOME`):

```json
{
  "hooks": {
    "SessionStart": [{ "hooks": [{ "type": "command", "command": "/path/to/filum-mcp brief" }] }],
    "UserPromptSubmit": [{ "hooks": [{ "type": "command", "command": "/path/to/filum-mcp log" }] }],
    "Stop": [{ "hooks": [{ "type": "command", "command": "/path/to/filum-mcp tidy" }] }]
  }
}
```

- **`filum-mcp brief`** puts the person in the context before the first message, with no tool call: their core, what
  was learned, the facts that hold now, the map of the memory and how much was said (at most 8,000 characters).
- **`filum-mcp log`** does two things with every message. It keeps the message in the log of events before the model
  reads it, so search and tidying find it even when the model wrote nothing. It also answers with what the memory
  holds about that message (at most 1,500 characters), so the agent remembers even when it does not think to search.
- **`filum-mcp tidy`** catches what a turn did not save. When more than 10 messages wait to be tidied, it asks the
  agent once per session, after its answer is shown, to call `memory_consolidate`. That writes who you are, your rules
  and your open items from what you said.
- **Never in the way.** No hook blocks a message: if one cannot read or write, it says why on stderr and lets
  everything through. `tidy` asks once per session to tidy before stopping, and only when much is waiting. Nothing
  private is in any answer.

| Host | The person at the start | Recall on every message |
|---|---|---|
| Claude Code, with the hooks | `filum-mcp brief` | `filum-mcp log`, and `filum-mcp tidy` at the end of a turn |
| Claude Code without hooks, Claude Desktop, Codex, Cursor | the instructions tell the model to call `memory_overview` first; when much is waiting to be tidied, it shows the newest of it | when the model searches |
| An app on the engine (`AddFilumEngine`) | `IMemoryBriefing.BriefAsync` | `IMemoryBriefing.RecallAsync` |

## Results, with their limits

The rounds below were written down before they ran: their questions, arms and pass criteria. Each is reported as it came
out, with the engine commit it ran on. The per-question results are not published in this repository. To run the same
benchmarks yourself, with your own key, see [`evals/bench/`](evals/bench/README.md).

**It knows who you are** (`bench.py knows-you`, specs 050 and 055). Each test person tells an agent about their life over
ten sessions, with changes, corrections, promises and rules. Then a fresh session answers 20 questions and 3 requests that
a rule should shape.

There are four people. People 1 to 3 were used while the engine was developed. Person 4 was written and frozen after
spec 055's changes and before any run on it.

Each arm answers through filum-mcp:
- **with the hooks**: `brief`, `log` and `tidy`, the setup the hooks section above describes;
- **without hooks**: the model has only the tools and their instructions;
- **with no memory**, for comparison.

Haiku 4.5 ran through Claude Code, judged by Claude Sonnet 5.5. gpt-4o-mini ran through OpenAI's API, judged by GPT-4o.
Engine `649f3df` (spec 055); person 4 again on `7c7d86e`.

| Model | Arm | Person 1 | Person 2 | Person 3 | Person 4 |
|---|---|---|---|---|---|
| Claude Haiku 4.5 | with the hooks | 22 / 23 | 22 / 23 | 20 / 23 | 22 / 23 |
| | without hooks | 17 / 23 | 19 / 23 | 17 / 23 | 18 / 23 (20 on `649f3df`) |
| gpt-4o-mini | with the hooks | 21 / 23 | 18 / 23 | 16 / 23 | 17 / 23 |
| | without hooks (engine `aa6a77a`) | 12 / 23 | 13 / 23 | 10 / 23 | not run |
| either | no memory (engine `aa6a77a`) | 2 / 23 | 2 / 23 | 2 / 23 | not run |

- **The thresholds.** Spec 050 registered, before its round, at least 20 with the hooks and 17 without, with no probe
  lost against its baseline (gpt-4o-mini on an earlier engine: 17 and 12). Spec 055 registered the same two numbers for
  Haiku 4.5 on person 4. Haiku 4.5 reaches both numbers on all four people; the "no probe lost" clause is not checked
  here. gpt-4o-mini does not reach them.
- **Against no memory,** the hooks arm gains on every person measured, for both models: exact McNemar p < 0.001.
- **The hooks cost little time.** `brief` and `log` take 0.2 to 0.4 seconds at the median and under 0.9 seconds at the
  95th percentile. The brief is under 2,000 characters, and each message's recall 600 to 1,100 on average.
- **Without hooks, Haiku reads about 1.35 times the input tokens** of the hooks arm, because it searches for itself.
- **Not re-run on this version.** Spec 056 changed filum-mcp's instructions after these rounds.

**Which setup to use:**

| Setup | Verdict |
|---|---|
| Hooks with a Haiku-class model or stronger | Recommended |
| No hooks with a Haiku-class model or stronger | Works, at more tokens |
| Hooks with a gpt-4o-mini-class model | Usable; it keeps fewer rules |
| No hooks with a gpt-4o-mini-class model | Not supported: it saves too little |

**MemoryAgentBench, FactConsolidation at 262k** ([Hu et al.](https://github.com/HUST-AI-HYZ/MemoryAgentBench), MIT): long
histories of facts that change.
- **The questions:** all 100 single-hop and 100 multi-hop.
- **The model:** gpt-4o-mini answers in every arm, one repetition. Engine `81fd1bb`.

| Arm | Single-hop | Multi-hop | All | Cost per answer |
|---|---|---|---|---|
| **Filum, one event per fact** | **88** | 10 | **98** | $0.0010 |
| BM25, the authors' protocol | 72 | 9 | 81 | $0.0056 |
| Mem0 2.2.1, run by us | 46 | 8 | 54 | $0.0003, plus its extraction |

- **Against Mem0:** +44 questions (exact McNemar 55 to 11, p = 3.6e-8).
- **Against BM25:** +17 questions (27 to 10, p = 0.008), all of it on single-hop. Multi-hop is level and low for every
  arm at this length.

**FactConsolidation at 64k, through Claude Code** (a replication with the isolated harness, written down before it ran).
The facts memory is built by Claude Haiku 5.5 from the history, told to save every fact with `fact_record`; Claude Haiku
4.5 answers in both arms; 50 single-hop and 50 multi-hop questions, one repetition. filum-mcp at `7c7d86e`.

| Arm | Single-hop | Multi-hop | Cost of the answers, in quota |
|---|---|---|---|
| **Filum, the facts the agent kept** | **47** | **38** | about $1 |
| BM25, the authors' protocol | 44 | 24 | about $10 |

- **Multi-hop:** +14 of 50 (exact McNemar 15 to 1, p = 0.0005). Chained questions are where keeping each fact with its
  changes pays off.
- **Single-hop:** level (4 to 1, p = 0.38).
- **Cost:** what the answers would cost at API prices; they ran on a Claude subscription's quota, not on paid calls.

**LongMemEval_S, through Claude Code** ([Wu et al.](https://github.com/xiaowu0162/LongMemEval), MIT): 50 questions, the
same setup, judged by Claude Sonnet 5.5. Filum 41, BM25 over the five best sessions 44: on par, not a significant difference (3 to 6, p = 0.51). Filum
is ahead on updated knowledge (9 to 6) and behind on questions that join several sessions (4 to 7).

**The limits, plainly:**
- **Four synthetic people, 23 probes each, one repetition.** "It knows who you are" is a small test. On person 3,
  Haiku is exactly at the thresholds. Rules are the weakest kind: a rule can reach the model and still be ignored.
- **One repetition** of FactConsolidation at 262k. It was planned with three: the per-question differences above are
  large, but a single run has no spread.
- **Mem0's configuration:** as shipped, except its extraction's output limit, raised to 16,000 tokens (with the
  default 2,000 it saved nothing). It was not tuned further.
- **The two Claude Code rounds are small:** 100 and 50 questions, one repetition each.
- **What is not here.** Larger rounds were run through Claude Code before its own memory and the account's identity
  were kept out of the model's context. Only their replications with the same isolation as `evals/bench` are reported
  above (made with the scripts this harness was written from); the comparisons with long context and Mem0 at 64k were
  not replicated.

## How it compares

| | Filum | Mem0 | Zep / Graphiti | Letta (MemFS) |
|---|---|---|---|---|
| **Who writes the memory** | The agent's own model, through tools; a later pass tidies it | An internal pipeline with its own model call (one ADD-only pass in 2026) | An internal model extracts entities and edges | The agent, through file tools; background agents consolidate |
| **Where it lives** | A local folder of plain files, with an append-only history of every change and its author | A vector store (and a graph, optionally), or the managed platform | A graph database (Neo4j or FalkorDB) | A git repository of Markdown files |
| **Facts that change** | A ledger: when each value held, when it was recorded, by whom, from which words, and how it ended (changed, corrected, retracted, ended); aliases | New and old memories kept side by side | Two timelines per edge (world time and record time) | The file's git history |
| **What a belief comes from** | The events it was said in, and a log of every message | Not exposed | Episodes | Commits |
| **Interface** | An MCP server (stdio), or a .NET library | REST and SDKs, and an MCP server | An MCP server, REST and SDKs | An SDK and file tools |
| **Needs a model, a service or a key of its own** | No | Yes | Yes | Yes |

Filum is for **one person's memory, on their own machine**, and it is measured in the open. It does not have:
- graph traversal beyond one step from a fact;
- multi-user hosting;
- a managed service.

## How it is built

The engine (`Filum.Engine`) is a .NET 10 library with no model of its own. It holds:
- the memory service: files, collections, facts, events and skills;
- the tools a model calls, with their rules in code;
- consolidation's rules.

It stores the memory through a port, implemented by a local folder (`Filum.Engine.Storage.LocalFolder`) and by an
in-memory store for tests. `filum-mcp` (`Filum.Mcp`) serves the tools over MCP. `Filum.Embeddings` adds the optional
local search model and reranker.

[`ARCHITECTURE.md`](ARCHITECTURE.md) has the layers, the entry points and the journey of a tool call;
[`docs/VISION.md`](docs/VISION.md) says what Filum is for.

## Upgrading

A memory folder records the format it is written in (`.filum/format`). An engine refuses a folder written in a newer
format, with an error that says so, instead of reading it.

**From a pre-release (`filum-mcp` 1.x, before this repository):**
- **Back up first:** copy the memory folder (`~/.filum` or `FILUM_HOME`) before you upgrade.
- **What changes:**
  - 1.0.0 writes format 3: each change to a file is stored as the lines that changed, so the history stays small, and every
    revision line carries the hash of its content, so a damaged history is an error instead of a wrong answer;
  - `/facts.csv` gains four columns at the end (spec 041);
  - `filum-mcp` offers 14 tools by default (`FILUM_TOOLS=full` for all).
- **Your folder:** a folder written by 1.x is read and moved on to format 3 when 1.0.0 opens it.
- **No downgrade:** 1.x engines do not check the format. Opened on a folder 1.0.0 has written to, they show changed
  files as empty. Go back only with the backup.

## Build and test

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), and nothing else: no model, no key, no Docker.

The projects, their layers and tests, the entry point and the journey of a tool call are in
[`ARCHITECTURE.md`](ARCHITECTURE.md); the coding rules are in [`docs/CODING-STANDARD.md`](docs/CODING-STANDARD.md).

```sh
dotnet build Filum.slnx
dotnet test Filum.slnx
```

Run the server from a clone with `dotnet run --project src/Filum.Mcp` (for example as the command an agent starts).
`bash scripts/smoke-mcp.sh <command>` checks that a build answers the MCP handshake.

The tool surface is checked against `tests/Filum.Engine.IntegrationTests/Snapshots/ToolCatalog.snapshot.json`. After a deliberate change,
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

## Use it with a model of your own

`filum-mcp` thinks with its host's model. To give the same memory to a model you call yourself, hand the engine's
tools to any `IChatClient` (Microsoft.Extensions.AI): the official SDKs of most providers, and local runtimes, offer
one. [`samples/Filum.Standalone`](samples/Filum.Standalone) is the whole of it, in a few lines:

```csharp
using var store = new LocalFolderStore(folder);
using var engine = FilumEngine.Create(store);
var tools = engine.Tools.Create(store.Owner, MemoryActor.Agent(session, Guid.NewGuid()));
using var client = new ChatClientBuilder(yourModel).UseFunctionInvocation().Build();
var answer = await client.GetResponseAsync(messages, new ChatOptions { Tools = tools.Tools });
```

- `FilumEngine.Create` takes any `IMemoryStore`: the local folder here, or a database of your own that passes
  `MemoryServiceContract` (`tests/Filum.Engine.Testing`). A host with a container calls `services.AddFilumEngine(...)`
  instead: see "Using it" in [`ARCHITECTURE.md`](ARCHITECTURE.md).
- Log what the person says with `engine.Memory.RecordAsync`, and run a consolidation pass with your model when it suits
  you: the rules are in `ConsolidationPrompt`, and the tools in consolidation mode (`engine.Tools.Create(..., consolidation: true)`).
- The search model is optional: `FilumEngine.Create(store, configure: e => e.UseE5Embeddings(folder))` with
  `Filum.Embeddings`, once `ModelFiles.EnsureAsync` has put the model in the folder.

## Evals

**Filum on public memory benchmarks, with your own key** (LongMemEval_S and MemoryAgentBench's
FactConsolidation, the benchmarks of the published rounds): one command downloads the pinned data, builds `filum-mcp`,
builds the memories, answers with Filum and the references, judges, and writes a result per question and a report
with the paired test, within a dollar cap you set. See [`evals/bench/README.md`](evals/bench/README.md).

```sh
pip install -r evals/bench/requirements.txt
OPENAI_API_KEY=... python evals/bench/bench.py longmemeval --model gpt-4o-mini --count 20 --cap 5
```

Measurements, not claims. `src/Filum.Evals` plays scenarios (JSON: a few messages, and what must be true after each)
and scores each model on tasks done, changes claimed but not made, rules kept, cost and speed. It calls real models,
so it runs by hand only, never in CI; the judge is `gpt-5.4-mini` with `OPENAI_API_KEY`.

**Against a service that hosts the engine** (any service that answers the same endpoint groups over HTTP):

```sh
dotnet run --project src/Filum.Evals -- --service <the host's url> --prefix <where it maps the groups> --register <its registration path> \
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
  - `--host <label>`: Filum on a host configured for that label, for example with one of its checks turned off, for an ablation; `--consolidate` runs a consolidation pass after each session;
  - `--baselines none,naive`: no memory, or BM25 over the sessions;
  - `--claude filum,none,full`: Claude Code with `filum-mcp`, with no memory, or with the whole history in the prompt.

## Roadmap

The engine, the local folder memory, `filum-mcp`, packages, the memory core with its log of events, imports from
files, the keep gate, search by meaning, trends and what Filum learns (done). Next: what is measured on LongMemEval
with the host's own model. The detailed specs are published alongside the code, in `specs/`.

## License

MIT. Contributions are welcome under the [Developer Certificate of Origin](DCO): see [CONTRIBUTING.md](CONTRIBUTING.md).
