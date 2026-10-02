# Lean turns — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — Shorter tools, two rules, no overview in a turn**
    - Do: the descriptions, the rules, the turn's tool list; the size bound; the snapshot.
    - Verify: `dotnet test Filum.slnx`; the catalog is 9,937 characters (from 14,516).

- [ ] **T2 — The measurement (about $0.60, with the owner's yes)**
    - Do: the engine's eval scenarios on the default model, and LongMemEval `oracle` (20 questions) on Gemma with `--consolidate`, as in spec 030.
    - Verify: criteria 3 and 4; the report under `evals/reports/`.

- [ ] **T-close — close the spec**
    - Do: check every acceptance criterion against the code and the tests; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update the index in `specs/README.md`.
    - Verify: `dotnet build Filum.slnx`, `dotnet test Filum.slnx`, `bash scripts/check-secrets.sh`.
