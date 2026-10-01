# The benchmark: LongMemEval on Filum — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — Reader, subset, protocol, judge, systems, run, report, CLI, download script**
    - Verify: `LongMemEvalTests` (criteria 2–4, 6–8); the download and its hashes (criterion 1).

- [x] **T2 — The first run (about $3, cap $4)**
    - Do: on `oracle`, 20 questions (4 per ability), the three small models; Filum, the two references, then the two ablations; then a few questions of `S` on Filum to measure the real cost of a full history.
    - Verify: the reports, committed under `evals/reports/`; criterion 5.
    - Done: `evals/reports/20261001-172311/` (oracle, 20 questions, 3 models, Filum, the two references, the two ablations and episodic memory, $3.54). The few questions of `S` were not run: the full histories are measured with the new memory core (spec 030).

- [x] **T-close — close the spec**
    - Do: criteria checked; Implemented; to `specs/done/`; README "Evals" and `CLAUDE.md`; index; private submodule and roadmap; push.
