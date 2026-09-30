# filum-mcp — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — The server**
    - Do: `src/Filum.Mcp` (store, per-call tools, refusals as errors, instructions, stderr logging, exit on a bad folder); packages; solution.
    - Verify: `dotnet build`; `dotnet run --project src/Filum.Mcp` answers an initialize request typed on stdin.

- [x] **T2 — End-to-end tests**
    - Do: `tests/Filum.Mcp.Tests` for criteria 1–7.
    - Verify: `dotnet test Filum.slnx` green.

- [ ] **T3 — Release**
    - Do: `scripts/smoke-mcp.sh`, `.github/workflows/release.yml`; README install and `CLAUDE.md`.
    - Verify: the smoke script passes locally on a `win-x64` single-file build; secret scan and private-name check clean; push; tag `v0.1.0` (the owner's yes); the release shows the five files (criterion 8).

- [ ] **T4 — By hand in Claude Code**
    - Do: download the Windows file from the release, register it with `FILUM_HOME` on a temporary folder, save a fact in one session and ask for it in a new one.
    - Verify: criterion 9; the outcome is written here.

- [ ] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update `specs/README.md`; update the submodule in the private repository and its roadmap.
    - Verify: `dotnet build`, `dotnet test`, `bash scripts/check-secrets.sh` pass here; `dotnet test` passes in the private repository.
