# Evals for any host — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — The runner in the public repository**
    - Do: move `Filum.Evals` and the scenarios; own auth DTOs; the expectation tests to `tests/Filum.Evals.Tests`; the private repository uses it.
    - Verify: public tests; private tests green, runner test included.

- [x] **T2 — Any host**
    - Do: `HostTarget`, `--prefix`, `--register`, `--person-header`, `--setup`, `--models host`, `--scenarios-dir`, the missing memory group.
    - Verify: runner tests against the sample host (criteria 3, 4, 5, 9).

- [x] **T3 — Guardrail expectations and the report**
    - Do: `tool`, `no_tool`, `surfaced`, `refused`, `host_unchanged`, `judge`; answered and refused counts.
    - Verify: expectation and runner tests (criteria 6, 10).

- [x] **T4 — The MCP growth test**
    - Do: `mcp` mode with two arms, `--max-sessions`, stream parsing, the memory read from the folder.
    - Verify: parsing and cap tests (criterion 7); one run by hand with both arms (criterion 8), its outcome written here.
    - Outcome (2026-10-01):
      - The run: `mcp --filum-mcp <Release filum-mcp.dll> --max-sessions 8 --scenarios "fact-recall*" --agent-model haiku`, with Claude Code and Haiku, 8 sessions; the judge cost $0.0012.
      - **with filum: 1/2.** `fact-recall-it` passed: the cat's name was saved in one session and found in the next.
        - In `fact-recall`, Haiku did not save "My favourite colour is teal.", a plain statement with no "remember". It called `memory_overview` and wrote nothing, then in the new session answered that it did not know.
        - This is a finding about the MCP instructions on small host models, not about the runner: worth a scenario sweep later.
      - **without: 0/2**, as expected. Both answers said they had no such information, and nothing was written anywhere.
      - Fixed after the run: the console showed `�` for symbols and printed costs with the system's decimal comma. The runner now writes UTF-8 and invariant numbers.
    - Changed during the task: the runner no longer references `filum-mcp` (that forced a newer `Microsoft.Extensions.AI.Abstractions` on every host that uses the runner), so `mcp` mode takes `--filum-mcp <path>` explicitly.

- [x] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update `specs/README.md`, the README, `CLAUDE.md` here and in the private repository; commit and push both.
    - Verify: `dotnet build`, `dotnet test`, the secret scan and the private-name check here; `dotnet test` in the private repository.
