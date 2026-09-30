# Hosting the engine — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied).

## Approach

1. **Move `Filum.Agent` as it is** to `src/Filum.Agent`:
   - the same namespaces and code;
   - its package versions added to this repository's `Directory.Packages.props`, with the same versions as the hosted product's so the two restore to the same packages.

   The hosted product removes its copy and references `engine/src/Filum.Agent`. The "`Filum.Agent` has no ASP.NET" test moves here.
2. **`src/Filum.Agent.Http`** (framework reference `Microsoft.AspNetCore.App`) holds what an ASP.NET host needs:
   - `AddFilumAgent<TContext>(services, configuration)`, which registers everything the turn needs from configuration: options, providers (the `ProviderConfiguration` of today), the model catalog, usage, conversations, the Postgres store on the host's `TContext`, memory, the package (spec 016), the reliability check and `AgentOptions`. The host registers its own `TContext` (a factory, as today) first;
   - `RequirePerson(group, Func<HttpContext, Guid?>)`, which replaces `RequireUserId`: the host says how to read the person;
   - the groups `MapFilumConversations`, `MapFilumUsage`, `MapFilumMemory`, `MapFilumSkills` and `MapFilumModels`, moved from `Filum.AgentService/Api` with the same routes relative to the group, and their DTOs (namespace `Filum.Agent.Http`).

   The hosted product maps them on `/api` with its JWT, so its routes stay byte-for-byte the same.
3. **Hosting points in `Filum.Agent`:**
   - **Options:** `AgentOptions` (`Agent:Name` = "Filum", `Agent:AllowModelChoice` = true, `Agent:TimeZone` = "UTC").
   - **Name:** `PlatformInstructions.Compose(…, name:)` swaps "You are Filum," for the name, and the agent loop uses the name.
   - **Host tools:**
     - `ITurnToolSource` (scoped) returns `IReadOnlyList<AIFunction>` from a `TurnContext(Person, ConversationId, MessageId, Services, Now, TimeZone, CancellationToken)`;
     - each host function is wrapped in a `DelegatingAIFunction` that times the call and records a `ToolStep` (kind `used`, the tool, the description from the tool's own description line, the duration, the error);
     - a host tool built with `HostTools.Create` (so its return value reaches the turn as it is) may return a `SurfacedResult(Text, Data)`, which gives the model `Text` and puts `Data` (a `JsonElement` of at most 4 KB) on the step;
     - an exception, or `Data` over the limit, gives a failed step and "the tool failed" to the model.
   - **Steps:** `ToolStep` gains `JsonElement? Data = null`, stored in the steps JSON and returned as the step DTO's `data`.
   - **Excluded tools:** `ExcludedTools` applies to host tools too.
   - **Name clashes:** `HostTools.CheckNames(IServiceProvider)` builds each source's tools once, with a synthetic context, and throws on a clash with the catalog or between sources. Hosts call it at start; a source only builds functions and calls nothing.
   - **Gate and observer:**
     - `ITurnGate` is called after validation and before anything is saved; `TurnGateResult.Refuse(status, message)` ends the turn with `TurnOutcome.Refused`, which the HTTP group maps to that status;
     - `ITurnObserver.AnsweredAsync(TurnContext, TurnUsage)` is called only on `Answered`.
   - **Model choice:** off means that `SendAsync` ignores `request.Model`.
   - **Usage:** `UsageService.GetSinceAsync(person, from)` is added beside the monthly one.
4. **`samples/Filum.SampleHost`**, a web app that:
   - uses `SampleDbContext` with `FilumModel` and `EnsureCreated` at start;
   - reads the person from the `X-Sample-Person` header, which is marked sample-only in code and README;
   - loads `packs/example`;
   - sets `Agent:Name` to "Sample";
   - registers a `ClockTools` source with `sample_now`, surfaced (`{"now": …, "timeZone": …}`);
   - maps all groups under `/sample`;
   - calls `HostTools.CheckNames` at start.
5. **`tests/Filum.Agent.Tests`** holds `WebApplicationFactory<SampleHost Program>` on a Testcontainers Postgres, with a fake chat client (as in the hosted product's tests), for criteria 3–10. Docker is needed; the CI's `ubuntu-latest` runner has it.
6. **Hosted product:**
   - `Program.cs` uses `AddFilumAgent<ConversationDbContext>` and the groups;
   - `Filum.Evals` uses the new DTO namespace;
   - its integration tests stay and must pass unchanged, which checks criterion 2.

Alternatives discarded:
- **One `MapFilumEndpoints`:** the first host needs groups one by one.
- **A clash check on every turn:** it finds the error late, in production.
- **SQLite for the libraries' tests:** `FilumModel` uses `jsonb` and identity columns.

## Constraints honored

- **The catalog snapshot is unchanged:** host tools are not in the catalog.
- **Every query is scoped by the person the host gives.**
- **No product name, host or machine** in the moved code; the private-name check runs before the push.

## Contract changes

- **New public libraries:** `Filum.Agent` and `Filum.Agent.Http`.
- **Configuration:** `Agent:Name`, `Agent:AllowModelChoice`, `Agent:TimeZone`.
- **Engine and agent API:** `ToolStep.Data`, `ITurnToolSource`, `TurnContext`, `SurfacedResult`, `ITurnGate`, `ITurnObserver`, `HostTools.Create`, `HostTools.CheckNames`, `MemoryTools.Record`, `UsageService.GetSinceAsync` and `MessagesSinceAsync`.
- **Payloads:** the step DTO gains `data`, optional. The hosted product's routes are unchanged.

## Files touched

| Where | Change |
|---|---|
| public `src/Filum.Agent/**` | moved, then the hosting points |
| public `src/Filum.Agent.Http/**` | new (endpoints and DTOs moved, DI setup) |
| public `samples/Filum.SampleHost/**`, `tests/Filum.Agent.Tests/**` | new |
| public `src/Filum.Engine/ToolStep.cs`, `PlatformInstructions.cs` | `Data`, name |
| public `Directory.Packages.props`, `Filum.slnx`, CI, README, `CLAUDE.md` | packages, projects, docs |
| private `Filum.Agent/`, `Filum.AgentService/Api/{Conversation,Memory,Skill,Model}Endpoints.cs`, `MemoryDtos.cs`, `UserIdFilter.cs`, `Config/ProviderConfiguration.cs`, `Data/FilumDb.cs` | removed (moved) |
| private `Filum.AgentService/Program.cs`, `*.csproj`, `Filum.slnx`, `Filum.Evals` | use the libraries |

## Risks and mitigations

- **The hosted product's API drifts in the move.** Its integration tests call every route and check the payloads, and they must pass unchanged.
- **Package version drift between the two `Directory.Packages.props`.** A downgrade shows as NU1109 at restore; keep them aligned.
- **Docker becomes needed for part of this repository's tests.** The engine's tests stay Docker-free, and the README and `CLAUDE.md` say which tests need it.

## Test strategy

| Criterion | Test |
|---|---|
| 1 | public build and test, with Docker |
| 2 | private integration tests unchanged; no `Filum.Agent` folder in private |
| 3 | sample host: `sample_now` step with `data`; the model's input has the text only |
| 4 | a clashing source: `CheckToolNames` throws at start |
| 5 | a throwing tool: failed step, answer still returned, no exception text in the payload |
| 6 | `Agent:Name` = "Sample": instructions begin "You are Sample"; the private tests still see "You are Filum" |
| 7 | no header: 401; two people: separate conversations |
| 8 | refusing gate: status and message, no model call, nothing saved; failing turn: no observer call |
| 9 | 5 KB data: failed step; 1 KB: still there when the messages are read again |
| 10 | model choice off: the requested model is ignored |
| 11 | snapshot test unchanged |
