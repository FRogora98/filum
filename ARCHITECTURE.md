# Architecture

This describes how the engine is built. What Filum is for is in `docs/VISION.md`, the coding rules are in
`docs/CODING-STANDARD.md`, and the approved exceptions to them are in `docs/EXCEPTIONS.md`.

## Projects

| Project | Layer | What it holds | Depends on |
|---|---|---|---|
| `src/Filum.Engine.Abstractions` | ports and models | the store and its five role interfaces, the models a host reads (files, revisions, events, facts, skills, imports), `MemoryOptions`, the constants (`ToolNames`, sensitivity, operations), `IEmbedder`, `IEventGate` | the runtime only |
| `src/Filum.Engine` | domain and application | `IMemoryService`, its eleven role interfaces (`Memory/Contracts`) and its internal services, the tools (`IMemoryToolsFactory`), imports, collections and compute, facts, skills, packages (`PackLoader`), consolidation rules, the instructions a host gives its model; `AddFilumEngine` and `FilumEngine.Create` | Abstractions, Jint, `Microsoft.Extensions.*` abstractions |
| `src/Filum.Engine.Storage.LocalFolder` | infrastructure | `LocalFolderStore`: plain files in a folder, with `.filum/` (state, revisions, events, vectors) written as its own records; `.UseLocalFolder(path)` | Abstractions, Engine |
| `src/Filum.Engine.Storage.InMemory` | infrastructure | `InMemoryMemoryStore`, for tests and short-lived uses; `.UseInMemoryStore()` | Abstractions, Engine |
| `src/Filum.Embeddings` | infrastructure | `E5Embedder` (multilingual-e5-small on ONNX), `KeepGate`, `UseE5Embeddings`: the optional local search model; `BgeReranker` (bge-reranker-v2-m3 on ONNX, spec 042): the optional local reranker | Abstractions, Engine |
| `src/Filum.Mcp` | app | `filum-mcp`: the engine as an MCP server over stdio, plus `filum-mcp log`, the hook that keeps what the person says | Engine, LocalFolder, Embeddings |
| `src/Filum.Evals` | app (run by hand, never in CI) | the eval runner, the growth test and the LongMemEval benchmark, with `evals/scenarios/` | Engine, the stores, Embeddings |
| `samples/Filum.Standalone` | sample | the engine with a model of your own: its tools given to any `IChatClient` | Engine, LocalFolder |

Dependencies point inward only: an app depends on the infrastructure, which depends on the engine, which depends on
the abstractions. No library depends on a web framework, a database, a model provider or an MCP host. A store depends
on the abstractions and on the engine's builder, which it extends.

Inside `Filum.Engine`, two pieces of infrastructure stay in the library until 2.0.0 (an approved
exception): the interpreter of `compute_run` (Jint, in `Collections/Compute/Jint`, behind the internal `ICodeSandbox`)
and the reading of a package's folder (`Packs/FileSystem`, behind the internal `IPackSource`). The rest of the engine
depends only on those interfaces.

## Tests

| Project | What it checks |
|---|---|
| `tests/Filum.Engine.Testing` | not a test project: `MemoryServiceContract`, the contract every store must pass, and the builders the engine's tests share |
| `tests/Filum.Engine.Abstractions.UnitTests` | the shared models and helpers: outcomes, actors, events, queries, the formats' constants |
| `tests/Filum.Engine.UnitTests` | the engine without I/O: the tools, the services, a package checked from memory |
| `tests/Filum.Engine.IntegrationTests` | the engine with files: packages, the tool catalog and parsing snapshots, the standalone sample |
| `tests/Filum.Engine.Storage.InMemory.UnitTests` | the contract on the in-memory store, and concurrent turns |
| `tests/Filum.Engine.Storage.LocalFolder.UnitTests` | the local folder's own logic with no disk: its lines and records, its layout, its state rebuilt from revisions |
| `tests/Filum.Engine.Storage.LocalFolder.IntegrationTests` | the contract on a local folder, the folder's own rules, a folder written by v0.7.1 |
| `tests/Filum.Embeddings.UnitTests`, `tests/Filum.Embeddings.IntegrationTests` | the keep gate; the search model and the reranker against references made in Python |
| `tests/Filum.Mcp.UnitTests`, `tests/Filum.Mcp.IntegrationTests` | filum-mcp's parts; the real server over stdio and its hook, on a temporary folder |
| `tests/Filum.Evals.UnitTests`, `tests/Filum.Evals.IntegrationTests` | the runner without I/O (every check, the runners against an in-memory host, the estimates, the command line, the benchmark's reader and scoring); the parts that write a folder |
| `tests/Filum.Engine.ArchitectureTests` | the rules below, on the built assemblies and on the sources |

The architecture tests fail when a library depends on a host, reads configuration or holds a storage implementation;
when a class is not sealed; when the engine exposes more than its entry points and models; when Jint is used outside
`Collections/Compute/Jint` or a file API outside `Packs/FileSystem`; and when a source file is over 300 lines, a method
over 50, or a file declares more than one top-level type (BUILD-02).

## Using it

```csharp
services.AddFilumEngine(options => configuration.GetSection(MemoryOptions.SectionName).Bind(options))
        .UseLocalFolder(path)              // or .UseStore<MyStore>() for a store of your own
        .UseE5Embeddings(modelFolder)      // optional: without it, search is by words only
        .UsePack(pack);                    // optional: a package's prompt, files and skills
```

- `IMemoryService` is the memory, and `IMemoryToolsFactory.Create(user, actor, …)` gives a turn its tools. Both are stateless singletons.
- A caller that needs only part of the memory depends on one of its roles, registered as the same instance: `IMemoryCore`,
  `IMemoryReader`, `IMemoryWriter`, `IMemorySkills`, `IMemoryHistory`, `IMemoryEvents`, `IMemoryFacts`, `IMemoryForgetting`,
  `IMemoryImports`, `IMemoryConsolidation`, `IMemoryBriefing` (`Filum.Engine.Memory.Contracts`). The engine's own tool groups, filum-mcp and the
  evals each use only the roles they need.
- Without a container, `FilumEngine.Create(store, options)` gives the same two services.
- A package is read with `PackLoader.TryLoadAsync`, which returns a refused package as a result with every problem
  (`PackLoadResult`); `PackLoader.LoadAsync` throws `PackException` instead.

## A tool call, end to end

1. The host's model calls a tool, for example `memory_append`.
2. `MemoryTools` passes the call to its tool group (`Tools/`). The group's `Run` checks the turn's call limit, times the
   call and records its step in the turn's `MemoryToolSession`.
3. The group validates the arguments and calls the memory, through the role it needs.
4. Every change ends in `MemoryWriter`, the single save path:
   - it checks the content and the core guard;
   - it saves through the store with an optimistic version check, so two changes from the same version cannot both win;
   - every change becomes a new revision.
5. The step goes back to the host; for the model, a refusal comes back as text.

Some changes are made by the engine itself rather than by a model's call: undo, rollback after a failed turn, forgetting a conversation, and consolidation. They all go through the same writer.

## The store contract

- **The contract:** any `IMemoryStore` must pass `tests/Filum.Engine.Testing` (`MemoryServiceContract`). Both stores here run it, and so does every host's own store.
- **The local folder:** its lines on disk are its own records (`EventLine`, `RevisionLine`), not the public models.
  `Storage.LocalFolder.IntegrationTests` keeps a folder written by v0.7.1 in `Fixtures/folder-v0.7`. Every version must
  read it, rebuild it without its state file, and keep writing lines of the same shape.

## The public surface

- **What is public:** only what hosts and stores use. Everything else is `internal`.
- **How it is tracked:**
  - each library's public API is listed in `PublicAPI.Shipped.txt`, and a change shows in `PublicAPI.Unshipped.txt`;
  - the tool catalog that models read is pinned by `tests/Filum.Engine.IntegrationTests/Snapshots/ToolCatalog.snapshot.json`.
- **Versions:** a breaking change raises the major version. What 1.0 shipped and the standard would place elsewhere
  stays until then, as recorded in `docs/EXCEPTIONS.md`.
