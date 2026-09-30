# Evals for any host — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [ ] **T1 — The runner in the public repository**
    - Do: move `Filum.Evals` and the scenarios; own auth DTOs; the expectation tests to `tests/Filum.Evals.Tests`; the private repository uses it.
    - Verify: public tests; private tests green, runner test included.

- [ ] **T2 — Any host**
    - Do: `HostTarget`, `--prefix`, `--register`, `--person-header`, `--setup`, `--models host`, `--scenarios-dir`, the missing memory group.
    - Verify: runner tests against the sample host (criteria 3, 4, 5, 9).

- [ ] **T3 — Guardrail expectations and the report**
    - Do: `tool`, `no_tool`, `surfaced`, `refused`, `host_unchanged`, `judge`; answered and refused counts.
    - Verify: expectation and runner tests (criteria 6, 10).

- [ ] **T4 — The MCP growth test**
    - Do: `mcp` mode with two arms, `--max-sessions`, stream parsing, the memory read from the folder.
    - Verify: parsing and cap tests (criterion 7); one run by hand with both arms (criterion 8), its outcome written here.

- [ ] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update `specs/README.md`, the README, `CLAUDE.md` here and in the private repository; commit and push both.
    - Verify: `dotnet build`, `dotnet test`, the secret scan and the private-name check here; `dotnet test` in the private repository.
