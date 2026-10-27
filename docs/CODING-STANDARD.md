# .NET backend coding standard

> English. The Italian twin is [`CODING-STANDARD.it.md`](CODING-STANDARD.it.md).

Version: 1.0 (2026-10-07). General guidelines, independent of the project.
Scope: .NET backend services and libraries (ASP.NET Core, minimal API, EF Core, Aspire). The frontend and the mobile
apps have their own standards.
In the example names, `Acme` stands for the product and `Orders`/`Billing` for the functional areas.

## How to use it

- Every rule has an ID, a level and a way to verify it.
  - **MUST**: required. An exception is written in `docs/EXCEPTIONS.md` with the rule, the file and the reason, and
    the technical lead approves it.
  - **SHOULD**: expected. A deviation is accepted only if it makes the code simpler, and it is explained in the PR or
    in the commit.
- Verification: **[auto]** = checked by the build, an analyzer or an architecture test in CI; **[review]** = checked
  in the audit.
- The restructuring goes in iterations:
  1. **Audit**: a table with one row per rule (followed / violated, file:line, number of violations).
  2. **Restructuring** of the violations, one area at a time. Behavior does not change and the tests stay green
     at every commit. Every commit cites the rule IDs.
  3. **New audit.**
  - The iteration closes when every MUST is followed and CI is green with all the [auto] checks.
- This document wins over habits, earlier documents and the style of the existing code. If a rule is wrong, it is
  changed here, not ignored in the code.

## 1. Solution and projects (SOL)

- **SOL-01 MUST** Project names are `<Product>.<Role>` (e.g. `Acme.Orders.Api`, `Acme.Billing.Core`). Folder names
  match project names. [review]
- **SOL-02 MUST** One web project per deployable service; `ServiceDefaults` and `AppHost` (Aspire) for the
  services. [review]
- **SOL-03 MUST** Dependencies between projects go in one direction only: service → libraries → abstractions. A
  library never references a service. [auto: architecture test]
- **SOL-04 MUST** The solution is split into the solution folders `src/` and `tests/` (plus `samples/` for
  libraries). [review]
- **SOL-05 MUST** Every production project has a `<Project>.UnitTests`; every service also has a
  `<Service>.IntegrationTests`. [review]

## 2. Architecture and dependencies (ARC)

Clean architecture: business rules do not depend on frameworks, databases, HTTP or model providers.

- **ARC-01 MUST** Every component has these layers, with dependencies pointing inward only:
  - `Domain`: entities, value objects, business rules, port interfaces. No dependency on EF Core,
    ASP.NET or external SDKs.
  - `Application`: use cases and application services. Depends only on `Domain`.
  - `Infrastructure`: implementations of the ports (persistence, HTTP clients, LLM providers, file system).
  - `Api` (services only): endpoints and DTOs. Calls only `Application`.
  [auto: architecture test]
- **ARC-02 MUST** In **libraries** the layers are **separate projects**
  (e.g. `Acme.Billing.Abstractions`, `Acme.Billing`, `Acme.Billing.Storage.Sql`). In **services** they are **folders** of a single project, with the direction enforced by the
  architecture tests. [review + auto]
- **ARC-03 MUST** Infrastructure is reached only through interfaces defined in the inner layer (ports and
  adapters). No `new` of external clients outside `Infrastructure` and the composition. [review]
- **ARC-04 MUST** One type per layer: persistence entity, domain model and DTO are different types, with
  explicit mapping written by hand (no AutoMapper). [review]
- **ARC-05 MUST** No per-request or per-run state in singletons. [review]

## 3. Composition and Program.cs (HOST)

- **HOST-01 MUST** `Program.cs` ≤ 100 lines. It holds only the startup sequence: `AddServiceDefaults`, the calls
  `Add<Product><Area>()`, `Use…`, `Map<Product>Endpoints()`, `Run`. No single registration, no
  endpoint, no business lambda. [auto: line count + review]
- **HOST-02 MUST** Registrations live in `Extensions/<Area>Extensions.cs`, one `static` class per area
  (`PersistenceExtensions`, `AuthenticationExtensions`, `ServiceExtensions`…). The public methods are named
  `Add<Product><Area>` and return `IServiceCollection` so they can be chained. [review]
- **HOST-03 MUST** Libraries expose a single public DI entry point (`services.AddAcmeBilling(…)`)
  with a builder or options for the variants. Their users do not register the internal classes by hand. [review]
- **HOST-04 SHOULD** Alternative implementations (null object, fakes for development) are chosen in the composition
  from the configuration, not with `if` statements scattered in the code. [review]

## 4. API (API)

- **API-01 MUST** Minimal APIs grouped by feature: `Api/<Area>/<Feature>/` containing
  `<Feature>Endpoints.cs` (group **and** routes) and `Dtos/`. A file name says what is inside: no
  `*Mapper.cs` that holds routes. [review]
- **API-02 MUST** Handlers are named `static` methods (e.g. `CreateOrder`), not inline lambdas, and stay
  thin: they read input and claims, call **one** application service, map the result. No business
  logic, no query, no try/catch. [review]
- **API-03 MUST** A single entry point `Map<Product>Endpoints()` that calls the `Map<Feature>Endpoints()`.
  [review]
- **API-04 MUST** Authorization with named policies, defined in one place and applied to the group. [review]
- **API-05 MUST** Input validation with a generic endpoint filter, not by hand in every handler. [review]
- **API-06 MUST** DTOs are `record` types with `required` members; DTOs never leave the `Api` layer. [review]
- **API-07 SHOULD** Routes are constants, not repeated strings. [review]

## 5. Services, classes and SOLID (SVC)

- **SVC-01 MUST** One class = one responsibility that can be described in one sentence without "and". If it takes
  two sentences, it is two classes. (SRP) [review]
- **SVC-02 MUST** File ≤ 300 lines, method ≤ 50 lines. **SHOULD** class ≤ 200 lines, method ≤ 25 lines.
  In a healthy codebase the median file is around 30-40 lines. [auto: analyzer]
- **SVC-03 SHOULD** At most 5 constructor dependencies; more signals a class that does too much. [auto]
- **SVC-04 MUST** Behavior that varies (by type, provider, chapter, channel) is extended by adding a
  class, not by changing an existing `if`/`switch`: a Strategy registered with keyed services or with
  `IEnumerable<T>`. (OCP)
  [review]
- **SVC-05 MUST** Subclasses honor the contract of the base; no `NotImplementedException` or empty methods
  to "satisfy" an interface. (LSP) [review]
- **SVC-06 MUST** Small interfaces, specific to their users; no "do-everything" interface (a class that implements many
  different interfaces must be split). (ISP) [review]
- **SVC-07 MUST** Code depends on abstractions, injected through the constructor (primary constructor). No service
  locator, no `static` with state, no `DateTime.Now`: use `TimeProvider`. (DIP) [review]
- **SVC-08 MUST** Every application service has an interface. If the feature folder has at most 8 files,
  the interface sits next to the implementation; above that, the interfaces go in the `Contracts/` subfolder (with
  its own namespace). The choice is made per folder, not per file. The ports to the infrastructure stay in the
  inner layer in any case (ARC-03). [review]
- **SVC-09 MUST** No copies of services per version (`ServiceV2`, `ServiceV3`): variants go through a
  Strategy or through versioned mappers. [review]

## 6. Data (DATA)

- **DATA-01 MUST** EF Core: `DbContext` and configurations only in `Infrastructure/Persistence`; one
  `IEntityTypeConfiguration<T>` per entity in `Configurations/`, applied with `ApplyConfigurationsFromAssembly`.
  [review]
- **DATA-02 MUST** No generic repositories on top of EF. Application services use specific ports
  (e.g. `IOrderStore`) implemented in `Infrastructure`; reusable queries become Specifications. [review]
- **DATA-03 MUST** Reads use `AsNoTracking()`. [review]
- **DATA-04 MUST** Table and column names as constants, never scattered strings. [review]

## 7. Configuration (CFG)

- **CFG-01 MUST** Every configuration section is a `sealed` class with the `Options` suffix, registered with
  `AddOptionsWithValidateOnStart<T>().Bind(GetSection(T.SectionName))` and validated (`Validate` or DataAnnotations).
  A wrong configuration stops the startup. [review]
- **CFG-02 MUST** Inject `IOptions<T>` (or `IOptionsMonitor<T>`), never `IConfiguration`, outside the
  composition. [auto: architecture test]
- **CFG-03 MUST** No secrets in the code, in `appsettings.json` or in the tests. Secrets come from Key Vault or
  environment variables. [auto: secret scanning]

## 8. Errors (ERR)

- **ERR-01 MUST** Expected business failures (not found, not allowed, limit exceeded) are a
  `Result`/`Result<T>` returned by the service, not exceptions. [review]
- **ERR-02 MUST** Exceptions are only for unexpected errors; a single central handler (`IExceptionHandler`) turns
  them into `ProblemDetails`. [review]
- **ERR-03 MUST** No `catch (Exception)` that swallows the error. A catch exists only if it adds context or
  really recovers. [review]

## 9. Logging (LOG)

- **LOG-01 MUST** `ILogger<T>` with structured messages (templates with placeholders, not interpolation). [auto]
- **LOG-02 SHOULD** Frequent logs, or logs on the hot path, use `[LoggerMessage]`. [review]
- **LOG-03 MUST** No personal data, secrets or conversation content in the logs. [review]

## 10. Design patterns (PAT)

- **PAT-01 MUST** A pattern is used when it solves a problem that exists now, not a future one. Every pattern has a
  recognizable name in the type (`…Strategy`, `…Factory`, `…Specification`, `…Handler`). [review]
- **PAT-02** The expected ones, with their use case:
  - **Strategy** for behaviors that vary (SVC-04);
  - **Factory** when creation depends on configuration or environment;
  - **Specification** for reusable queries (DATA-02);
  - **Null object** for optional dependencies;
  - **Result** for expected failures (ERR-01);
  - **Template method** for families of handlers with the same skeleton;
  - **Decorator** for cross-cutting behaviors (retry, cache, metrics) without touching the decorated class.
- **PAT-03 MUST** No misused patterns: interfaces with a single implementation that never needs to be
  replaced in tests, factories that only call `new`, layers of indirection with no reason. [review]

## 11. C# code (CS)

- **CS-01 MUST** `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` in every project,
  no nullability warning suppressed without a reason. [auto]
- **CS-02 MUST** File-scoped namespaces; namespace = folder path. [auto]
- **CS-03 MUST** One public type per file, the file named after the type. [auto]
- **CS-04 MUST** Classes are `sealed` by default; `sealed` is removed only for intended inheritance. [auto]
- **CS-05 MUST** Every asynchronous method receives and passes on a `CancellationToken ct`. [auto]
- **CS-06 MUST** No magic strings or magic numbers: constants in nested static classes (`Constants.X.Y`).
  [review]
- **CS-07 SHOULD** `partial` only to split a large type by concern (`X.cs`, `X.Validation.cs`), never to
  hide a class that should be split. [review]
- **CS-08 MUST** Names in English that say what the thing does. No abbreviations, no generic `Helper`/`Utils`/
  `Manager`. [review]
- **CS-09 MUST** No dead code, commented-out code or `TODO` without a reference to a backlog item.
  [auto: analyzer]

## 12. Comments and documentation (DOC)

- **DOC-01 MUST** The code explains itself through names and structure. An inline comment explains **why**, never
  **what**. No comments that repeat the method name, no change history in comments. [review]
- **DOC-02 MUST** XML `<summary>` only on: the public API of libraries (required, one or two sentences), service
  interfaces, options classes. No empty `<param>`. [auto for the public API: CS1591]
- **DOC-03 MUST** Repository documentation is essential and free of duplicates, each fact in one place only:
  - `README.md`: what it is, how to start it, links to the other documents;
  - `ARCHITECTURE.md`: projects, layers, main flows;
  - `docs/`: only flows and decisions that the code does not show.
  [review]
- **DOC-04 MUST** A document that no longer matches the code is fixed or deleted in the same commit
  that changes the code. [review]

## 13. Tests (TEST)

- **TEST-01 MUST** The folder structure of the tests mirrors that of the code. [review]
- **TEST-02 MUST** Test names follow `Method_WhenCondition_ShouldResult`, in English.
  [review]
- **TEST-03 MUST** Test classes are named `<TestedClass>Tests`. Unit tests without I/O; integration tests with
  `WebApplicationFactory` and Testcontainers shared through a collection fixture. [review]
- **TEST-04 MUST** Fakes and builders shared in one place per test project (`Fakes/`, `Builders/`), not
  duplicated per file. [review]
- **TEST-05 MUST** Architecture tests (NetArchTest or ArchUnitNET) verify ARC-01, SOL-03, CFG-02 and run in
  CI. [auto]
- **TEST-06 SHOULD** A fake `TimeProvider` for time, Bogus or builders for data. [review]

## 14. Build and quality (BUILD)

- **BUILD-01 MUST** Central package management (`Directory.Packages.props`,
  `ManagePackageVersionsCentrally`) and a shared `Directory.Build.props` (target framework, nullable, analyzers). [review]
- **BUILD-02 MUST** `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, a complete `.editorconfig` with
  the rules of this document that can be automated. [auto]
- **BUILD-03 MUST** CI runs the build, unit tests, integration tests and architecture tests on every push, and
  blocks the merge if they fail. [auto]
- **BUILD-04 SHOULD** Coverage measured (coverlet) and published in CI. [auto]

## 15. Libraries and SDKs (SDK)

- **SDK-01 MUST** Everything is `internal` by default; only what the library's users need is `public`. [review]
- **SDK-02 MUST** The public API is tracked (`PublicApiAnalyzers`, `PublicAPI.Shipped.txt` file): every change
  to the public API is visible in the diff. [auto]
- **SDK-03 MUST** A `*.Abstractions` project with only the interfaces and models that a host or a plugin must
  know (e.g. the storage interface); the implementations live in separate packages. [review]
- **SDK-04 MUST** Semantic versioning; a breaking change to the public API raises the major version. [review]
- **SDK-05 MUST** A project in `samples/` that shows the minimal usage in a few lines and compiles in CI. [auto]
- **SDK-06 MUST** No dependency on a specific host (ASP.NET, Aspire, a VM): the library can also be used from a
  console. [auto: architecture test]

## 16. Microservices (MS)

Applies when the system is made of several services deployed independently. Sections 1-15 describe the inside of
each service; this one describes how the services live together.

- **MS-01 MUST** Every service owns its data: one database (or schema) per service, and no other service reads or
  writes it directly. Other services' data is reached only through their API or their events. [review]
- **MS-02 MUST** A service matches a business area (bounded context), not a technical layer. No
  "entities-only" or "utilities-only" services. [review]
- **MS-03 MUST** No shared library with domain models. Only contracts (DTOs, events) in versioned packages and
  cross-cutting infrastructure (`ServiceDefaults`, authentication) are shared. [auto: architecture test]
- **MS-04 MUST** Versioned contracts: APIs with the version in the route or in a header, events with the version in
  the schema. A breaking change creates a new version; the old one stays as long as it has consumers. [review]
- **MS-05 MUST** Every HTTP call between services goes through a typed client with a timeout, retry with backoff and a
  circuit breaker (`Microsoft.Extensions.Http.Resilience`). No call without a timeout. [review]
- **MS-06 SHOULD** Asynchronous communication (messages, events) when the caller does not need the answer right away;
  synchronous only for the reads needed to answer. [review]
- **MS-07 MUST** Message handlers are idempotent: the same message received twice does not produce double
  effects. [review]
- **MS-08 MUST** A state change and the publication of its event happen together (outbox pattern), never
  "save, then publish" in two independent steps. [review]
- **MS-09 MUST** Observability shared by all services through `ServiceDefaults`: OpenTelemetry for traces, metrics
  and logs, a correlation id propagated in HTTP and messages, separate liveness and readiness health checks. [review]
- **MS-10 MUST** Authentication between services too (service tokens or managed identity); no trust based only
  on the internal network. [review]
- **MS-11 MUST** Every service is built, tested and deployed on its own: its own pipeline, no required coordinated
  deploy. [review]
- **MS-12 SHOULD** Contract tests between producer and consumer (e.g. Pact) for shared APIs and events.
  [auto]
- **MS-13 MUST** No distributed transactions. Processes that touch several services use a saga (orchestrated or
  choreographed) with explicit compensating actions. [review]

## 17. Basic choices and their reasons

- **Minimal API** instead of controllers: less ceremony, groups by feature, composable endpoint filters.
- **Layers as projects** in libraries (the compiler enforces the boundaries and the public API is checked), **as
  folders** in services (fewer projects to manage, boundaries enforced by the architecture tests).
- **Result for expected failures** instead of exceptions: the flow is explicit in the signature and exceptions are
  kept for the truly unexpected cases.
- **Interfaces next to the implementations** in small folders, in `Contracts/` in large ones (SVC-08).

To avoid even if common in many codebases: secrets in the code, files thousands of lines long, copies of services per
version, block-scoped namespaces, no analyzers.
