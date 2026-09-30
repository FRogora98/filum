# Hosting the engine — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [ ] **T1 — `Filum.Agent` in the public repository**
    - Do: move it unchanged; add the packages; the references test; the private repository references it from the submodule.
    - Verify: public build and engine tests; private `dotnet test` green, unchanged.

- [ ] **T2 — `Filum.Agent.Http`**
    - Do: `AddFilumAgent`, `RequirePerson`, the five groups and their DTOs; the private `Program.cs` uses them; `Filum.Evals` usings.
    - Verify: private `dotnet test` green, unchanged (criterion 2).

- [ ] **T3 — Hosting points**
    - Do: `AgentOptions`, the name, host tools and surfaced results, `ToolStep.Data`, clash check, gate, observer, model choice, usage since a date.
    - Verify: builds; private tests green (their instructions still say "You are Filum").

- [ ] **T4 — Sample host and its tests**
    - Do: `samples/Filum.SampleHost`; `tests/Filum.Agent.Tests` for criteria 3–10; CI runs them.
    - Verify: public `dotnet test` green with Docker; public CI green.

- [ ] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update `specs/README.md`, the README, `CLAUDE.md` (here and in the private repository); commit and push both repositories.
    - Verify: `dotnet build`, `dotnet test`, `bash scripts/check-secrets.sh`, `scripts/check-public.sh` here; `dotnet test` in the private repository.
