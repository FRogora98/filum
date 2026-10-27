# Hybrid retrieval — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — The retrieval measure, words only**
    - Verify: 470 questions of `S`: recall@5 93.8%, answer sessions found @10 86.7%.

- [x] **T2 — Vectors on every store; the fused ranking; `Filum.Embeddings`**
    - Verify: `MemoryServiceContract`, `HybridSearchTests`, `E5EmbedderTests`.

- [x] **T3 — `filum-mcp` and the hosts**
    - Verify: `ServerTests` (the model off), the download test; consolidation passes make vectors.

- [x] **T4 — The measurement with the model**
    - Verify: criterion 5 on the 470 questions; the report committed under `evals/reports/`.

- [x] **T-close — close the spec**
    - Do: check every acceptance criterion against the code and the tests; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update the index in `specs/README.md`.
    - Verify: `dotnet build Filum.slnx`, `dotnet test Filum.slnx`, `bash scripts/check-secrets.sh`.
