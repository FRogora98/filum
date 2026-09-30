# Hosting the engine

> Status: Reviewed
> Area: engine / hosting
> Author/date: federico rogora (drafted by Claude), 2026-09-30
> Roadmap: 017, designed with the first product that hosts the engine (sync point S3)

## Problem

A product that wants a vertical agent with Filum's memory (the first is a closed finance app) must run the engine inside its own service. It has its own login, its own database and domain tools that no package can express as data, and it wants no second service and no fork.

Today it cannot:
- **The turn is not public.** The loop (`Filum.Agent`: instructions, the agent loop, the reliability check, the model catalog and providers, conversations, usage, the Postgres store) lives only in the hosted product's private repository. The owner decided on 2026-09-30 that it becomes public ("Diventa pubblico").
- **The turn offers only the engine's tools**, so a host has no place to add its own.
- **The assistant is called Filum in code** ("You are Filum", the agent's name), whatever the product is.
- **The HTTP endpoints** (conversations, memory, skills, models, usage) belong to one host and cannot be mapped by another.

## Goal

A product hosts the engine as libraries from this repository:
- it registers its own tools beside the engine's;
- its tools' results can travel in the turn's steps for its app to show;
- it names its assistant;
- it keeps its own login and database;
- it maps the engine's endpoints where it wants.

A sample host in this repository does all of it, and the hosted product that already uses the engine becomes one more host of the same libraries.

## Non-goals

- **No change to the engine's tools, their names or their meaning.** The catalog snapshot does not move.
- **No accounts or login in the libraries:** the host decides who the person is and passes the person's id.
- **No versioned public HTTP contract yet:** the endpoints are mapped as they are today, and versioning them is spec 019.
- **No evals for other hosts:** spec 018.
- **No NuGet packages:** hosts use this repository as a git submodule, as the hosted product already does. Packages come when a host needs them.
- **No streaming turns.**
- **No real product's tools or rules in this repository:** only the sample host's, domain-free.

## Current behavior

- **`Filum.Agent`** (private repository, about 1,400 lines, no ASP.NET, no private names in it):
  - `ConversationService.SendAsync(userId, conversationId, request)` runs a turn;
  - the instructions are `PlatformInstructions.Compose(…)`, with the package's prompt since spec 016;
  - the tools are `MemoryTools.Tools` only;
  - the agent is named "Filum".
- **Steps** are `ToolStep` records (kind, tool, path, description, duration, revision, error, rows). They are stored as JSON with each answer and returned to the app.
- **The database is already the host's:** `FilumModel.Configure(modelBuilder)` adds the engine's tables to a context the host owns, and the host keeps the migrations; `IFilumDb` gives short-lived contexts.
- **The endpoints** are in `Filum.AgentService/Api/*`, under `/api`. `RequireUserId()` reads the user from the JWT.
- `PlatformInstructions.Text` begins "You are Filum, the person's own assistant."

## Desired behavior

**The libraries in this repository**
- **`src/Filum.Agent`:** the turn, moved here with its namespaces and behavior unchanged, MIT like the engine. Nothing in it names a product or a machine.
- **`src/Filum.Agent.Http` (new):** the endpoint groups as extension methods on a route group, with the same paths and payloads as today:
  - conversations and messages, with skill proposals accepted or declined;
  - memory files, revisions and undo;
  - skills;
  - models;
  - usage.

  Each group is mapped on its own: a host maps only the groups it wants, under the prefix it wants, and passes how to read the person's id from a request. A request without a person gets 401. Accounts stay in each host.
- **The model choice** is the host's: with `Agent:AllowModelChoice` off, the model a request names is ignored, and every turn uses the host's default model.
- **The hosted product's service** keeps its accounts, its context and its migrations. It maps these groups under `/api` with its JWT, so its app sees exactly the same API.

**Host tools**
- A host registers tools for the turn with a **turn tool source**. It is a scoped service, resolved from the request's scope, so a host's tools can use its own scoped services (for example a current-user service and repositories that filter by person). For each turn it gets a **turn context**:
  - the person, the conversation and the message;
  - the request's service provider;
  - the turn's cancellation token, which reaches every tool call;
  - "now", from the host's `TimeProvider`;
  - the person's time zone, as the host configures it (`Agent:TimeZone`, an IANA name, default UTC).

  It returns tools built for that turn.
- Its tools are offered to the model beside the engine's, and they may be subject to the same `ExcludedTools`.
- A name that clashes with an engine tool, or with another host tool, stops the host at start with a clear message.
- Each call of a host tool becomes a **step** of the turn, like the engine's: its kind, the tool, a one-line description, the duration, and an error when it failed.
- A host tool may return a **surfaced result**: text for the model, plus a small JSON value for the app. The value is kept with the step (`data`) and returned with the answer's steps, so the app can show it, for example as a card. The model never sees `data`, only the text.
- `data` is a new optional field of the stored steps and of the steps in the answer's payload: additive, so an app that does not know it ignores it.
- `data` is stored with the step, so it comes back when the conversation is read again, not only in the live answer. It is at most 4 KB; a larger value makes the call a failed step ("the result was too large"), so a host sees the problem at once.
- A host tool that throws is a failed step. The model gets "the tool failed" and the turn goes on.

**The assistant's name**
- `Agent:Name` (default "Filum") names the assistant in the platform instructions ("You are <name>, the person's own assistant…") and in the agent loop.
- Everything else in the platform text stays the same.

**Rules** keep working as they do now:
- the platform layer first;
- then the package's prompt (spec 016);
- then the person's core.

Beyond the prompt, a host enforces what must never happen in code:
- it leaves engine tools out with `ExcludedTools`, the memory-writing tools too;
- its own tools can propose instead of write.

The platform text forces no language: it tells the agent to answer in the language of the person's message, and a package's prompt can say more.

**Before and after a turn**
- A host may register a **turn gate**, called before the model with the person and the conversation. It can refuse the turn with its own message and HTTP status (for example 402 when the person's plan has no messages left). Then the model is not called, nothing is saved, and nothing is spent.
- A host may register a **turn observer**, called only after a turn that answered, with the turn's usage (model, tokens, cost). A failed turn calls no observer, so a host that counts messages never counts one that failed.
- Usage per person stays readable from code (`UsageService`) for a month or from a given date, not only through the usage group.

**The sample host** (`samples/Filum.SampleHost`, domain-free) is a small ASP.NET app that:
- has its own context with `FilumModel`, and creates its tables at start (a real host keeps migrations, as the hosted product does);
- identifies the person in the simplest way that is not a real login, clearly marked as sample-only;
- loads the example package;
- names its assistant;
- registers one tool whose result is surfaced (the current date and time, as text for the model and a JSON value for the app);
- maps the endpoint groups under a prefix of its choice.

**Tests**
- The libraries' tests run here against Postgres in a container: this repository's CI and a developer's machine need Docker for them. The engine's own tests still need nothing.
- The hosted product keeps its own integration tests.

## Platform check

1. **Generic:** yes. The hosting points know no domain, and the sample host's tool (the time) and package (a journal) name none.
2. **Sensitivity and privacy:**
   - every query stays scoped by the person's id, which only the host gives;
   - host tools see only the person of their turn;
   - sensitivity is still enforced by the engine;
   - `data` is shown to the person's own app only;
   - no product's code, rules or data enter this repository.

## Acceptance criteria

1. Given this repository, when it is built and tested (with Docker for the libraries' tests), then `Filum.Agent`, `Filum.Agent.Http` and the sample host build, and their tests pass. The engine's tests still pass with no Docker.
2. Given the hosted product, when it uses `Filum.Agent` and `Filum.Agent.Http` from the submodule, then it has no copy of them, its app's API is unchanged (same paths and payloads), and its tests pass.
3. Given the sample host with its tool, when a turn calls it, then the answer's steps include that call with its description and its `data`, and the model's input contains the text but not `data`.
4. Given a host tool named like an engine tool, when the host starts, then it stops with a message naming the clash.
5. Given a host tool that throws, when a turn calls it, then the step is failed, the turn still answers, and nothing of the exception's detail reaches the person.
6. Given `Agent:Name` set to another name, when a turn's instructions are composed, then they begin "You are <name>"; without it they begin "You are Filum".
7. Given a request to a mapped group with no person, when it is sent, then the answer is 401. Given two people, when each lists conversations and reads messages through the mapped groups, then neither sees the other's.
8. Given a turn gate that refuses, when a message is sent, then the host's status and message come back, the model is not called and no message or usage is saved. Given a turn that fails, then no observer is called.
9. Given a host tool whose surfaced value is over 4 KB, when it is called, then the step is failed. Given one within the limit, when the conversation is read again, then the step still carries its `data`.
10. Given `Agent:AllowModelChoice` off, when a request names another model, then the turn uses the host's default model.
11. Given the catalog snapshot, when hosting is added, then the snapshot is unchanged.

## Open questions

None.
