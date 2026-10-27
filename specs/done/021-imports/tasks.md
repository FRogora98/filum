# Absorb: imports from files — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — The parsers**
    - Do: `Imports.cs`: WhatsApp (Android, iOS, date order, continuation lines, notices), mbox (headers, folding, multipart, quoted-printable, base64, HTML stripped), ICS (unfolding, escapes, dates), CSV (date column); detection; identity.
    - Verify: `ImportsTests` (criteria 1–4).

- [x] **T2 — The batch append, on every store**
    - Do: `AppendEventsAsync` in memory, the local folder and Postgres.
    - Verify: a contract test on every store.

- [x] **T3 — The import**
    - Do: `MemoryService.ImportAsync` (limits, duplicates, preview, sample, source id, sensitivity).
    - Verify: contract tests (criteria 5–9).

- [x] **T4 — `memory_import` in `filum-mcp`**
    - Do: the tool and a line in the MCP instructions.
    - Verify: `Filum.Mcp.Tests` (criterion 10).

- [x] **T5 — `POST …/memory/import`**
    - Do: the endpoint, the DTO, the OpenAPI snapshot, the API readme.
    - Verify: the hosting service's tests (criterion 11), `OpenApiTests`.

- [x] **T-close — close the spec**
    - Do: check every acceptance criterion against the code and the tests; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update the index in `specs/README.md`; README; the roadmap of the hosted product.
    - Verify: `dotnet build Filum.slnx`, `dotnet test Filum.slnx`, `bash scripts/check-secrets.sh`.
