# Packages — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — The format and the loader**
    - Do: `Pack`, `PackException`, `Pack.Load` with the shape checks and the dry run; `packs/example/`.
    - Verify: `PackTests` for criterion 3 and for loading the example.

- [x] **T2 — A new memory with a package**
    - Do: `MemoryService` optional pack; `EnsureCoreAsync` applies it; `PackSection`, `Compose`, overview.
    - Verify: `PackTests` for criteria 1, 2, 4, 5, 6; snapshot unchanged (criterion 8); all engine tests green.

- [x] **T3 — filum-mcp**
    - Do: `FILUM_PACK`; instructions with the pack section.
    - Verify: `ServerTests` for criterion 7 and for the pack prompt in the instructions.

- [x] **T4 — The hosted service** (private repository)
    - Do: `Memory:PackPath`; `Compose` with the pack; one integration test.
    - Verify: `dotnet test` in the private repository.

- [x] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update `specs/README.md`, the README, `CLAUDE.md`; release `v0.2.0`; update the private submodule and roadmap.
    - Verify: `dotnet build`, `dotnet test`, `bash scripts/check-secrets.sh` here; `dotnet test` in the private repository.
