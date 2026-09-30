# <Feature name> — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [ ] **T1 — <short name>**
    - Do: <the change, concrete enough that someone else could carry it out>
    - Verify: <command, test or observable result, e.g. "dotnet test, new XTests pass">

- [ ] **T2 — <short name>**
    - Do: <...>
    - Verify: <...>

- [ ] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion against the code and the tests; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update the index in `specs/README.md`.
    - Verify: the backend and mobile verification commands in `CLAUDE.md` pass.
