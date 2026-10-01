# Stable endpoints for apps — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — Metadata, header, description**
    - Do: route metadata, `FilumApi`, the header filter, OpenAPI in the sample host, the snapshot and its test, `docs/api/README.md`.
    - Verify: public tests (criteria 1, 2, 3, 6).

- [x] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion; Implemented; move to `specs/done/`; README, `CLAUDE.md`, index; private submodule, roadmap, index; push both.
    - Verify: public and private `dotnet test` (criterion 4).
