# Hybrid retrieval — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (delegated by the owner).

## Approach

1. **Measure first, at no cost.** `longmemeval --retrieval` plays the search alone: each question's history becomes the events of a fresh in-memory store, the question is searched, and a hit counts when it is in a session holding the answer. The baseline (words only) is 93.8% recall@5 and 86.7% of the answer's sessions in the first 10.
2. **The model, outside the engine.** `Filum.Embeddings` holds `E5Embedder`:
   - `Microsoft.ML.Tokenizers`' SentencePiece with XLM-R's ids;
   - ONNX Runtime;
   - mean pooling, unit length.

   It also holds `ModelFiles`: pinned revision, SHA-256, downloads to a `.part` file first. The engine only knows `IEmbedder`.
3. **Vectors as derived data in every store:**
   - `IMemoryStore.SaveVectorsAsync` and `VectorsAsync`, with `ForgetAsync` removing them;
   - stored in memory, in `.filum/vectors.jsonl` (base64 floats), and in the table `memory_event_vectors` (`real[]`).
4. **Ranking.** `EventSearch.Fuse` combines BM25's top 50 and the cosine's top 50 by reciprocal rank (k = 60).
5. **Where vectors are made.**
   - `MemoryService.MakeVectorsAsync` makes them in the background: in consolidation passes, and in `filum-mcp` after an import or a tidying.
   - A search makes at most 32 itself.
   - Texts are cut at 256 tokens and batched by length, which made a 500-message history about four times faster.

**Discarded:**
- **A vector database or pgvector:** a person's events are thousands, not millions, and a scan is milliseconds.
- **Making vectors at search time:** 500 messages took 13 to 55 seconds.
- **An English-only model:** the owner writes in Italian.
- **The model inside the engine or the hosting service's HTTP layer:** every host would ship ONNX Runtime.

## Contract changes

- **Engine:**
  - `IEmbedder`;
  - `MemoryVectors`;
  - `IMemoryStore.SaveVectorsAsync` / `VectorsAsync`;
  - `MemoryService(…, IEmbedder? embedder = null)`;
  - `MemoryService.MakeVectorsAsync`;
  - `MemoryOptions.VectorsPerSearch` (32).
- **New library:** `Filum.Embeddings` (`E5Embedder`, `ModelFiles`).
- **Postgres:** the table `memory_event_vectors` (`event_id` key, `user_id`, `vector real[]`); hosts add the migration.
- **`filum-mcp`:**
  - `FILUM_MODELS` (where the model lives);
  - `FILUM_EMBEDDINGS=off`;
  - the model is downloaded in the background on first start.
- **Runner:** `longmemeval --retrieval [--embeddings <folder>]`.
- **Tools and endpoints:** none.

## Risks and mitigations

- **A 120 MB download on a person's machine.** It happens once, in the background, is checked by hash and can be turned off. The release's smoke test turns it off.
- **ONNX Runtime has no native library for one platform** (osx-x64). The model does not load there, and the search falls back to words.
- **Rollback:** without an `IEmbedder`, everything works as before.

## Test strategy

| Criterion | Test |
|---|---|
| 1 | `E5EmbedderTests` against `Fixtures/e5-reference.json`, where the model is |
| 2 | `HybridSearchTests` (a fake model) |
| 3 | `MemoryServiceContract.Vectors_are_kept_by_event_and_forgotten_with_it`, on every store |
| 4 | `ServerTests` with the model off; `E5EmbedderTests.A_download_that_does_not_match_its_hash_leaves_no_file` |
| 5 | `longmemeval --retrieval --embeddings`, by hand, on the 470 questions of `S` |
