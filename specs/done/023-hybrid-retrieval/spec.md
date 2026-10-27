# Hybrid retrieval: what was said, found by meaning too

> Status: Implemented (2026-10-04). Reviewed (the owner delegated the specs of phase E, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (search) / MCP / hosting / evals
> Author/date: federico rogora (drafted by Claude), 2026-10-04
> Roadmap: phase E, spec 023 ("recall@k on the benchmark rises against lexical search"). New dependencies approved by the owner on 2026-10-01: ONNX Runtime, the embedding models, each optional.

## Problem

`events_search` ranks by words (BM25). It is measured, at no cost, on all 470 non-abstention questions of LongMemEval `S`, about 500 events each, with the search alone:

- a session holding the answer is among the first 5 hits for 93.8% of the questions;
- but of **all** the sessions holding the answer, only 86.7% are among the first 10;
- the gap is in the questions that need several sessions: multi-session 82.0%, temporal 81.1%.

A word search misses what was said in other words ("bought a fern" for "plants"), and other languages than the query's.

## Goal

`events_search` also finds by meaning, with a small local model, so that more of the answer's sessions come up. Without the model, it works as today.

## Non-goals

- **No GPU, no paid embeddings, no vector database:** a small multilingual model on the CPU, vectors next to the events.
- **No change to the tools' surface:** `events_search` keeps its name and parameters; what changes is its ranking.
- **Files are not embedded:** only the log of events. `memory_search` stays a text search over the files.

## Desired behavior

1. **The model.**
   - `multilingual-e5-small`: MIT license, 384 dimensions, quantized to about 118 MB, many languages.
   - It runs through ONNX Runtime with its SentencePiece tokenizer, in a separate library, `Filum.Embeddings`, so that the engine itself takes no new dependency.
2. **Vectors.**
   - An event that was said, answered or imported gets a vector, kept beside the events: `.filum/vectors.jsonl` in a folder, the table `memory_event_vectors` in Postgres.
   - Vectors are made in the background: by a consolidation pass in a host, after an import or a tidying in `filum-mcp`. A search makes at most 32 missing ones (`Memory:VectorsPerSearch`), so it stays quick: embedding is about 25 ms per message on a laptop's CPU.
   - Vectors are derived data. Forgetting an event forgets its vector, and a lost vector is made again.
3. **The ranking.**
   - With a model, `events_search` fuses two rankings by reciprocal rank (k = 60): the words' (BM25) and the meaning's (cosine of the query's vector against the events').
   - The date window and private events follow the same rules as today.
4. **Where the model comes from.**
   - **`filum-mcp`:** in `~/.filum/models/multilingual-e5-small` (or `FILUM_MODELS`), downloaded in the background on first start from Hugging Face, checked against its SHA-256. The search uses it once it is there. `FILUM_EMBEDDINGS=off` turns it off.
   - **A host:** registers the model as the engine's `IEmbedder` (`services.AddSingleton<IEmbedder>(new E5Embedder(folder))`, from `Filum.Embeddings`), so that a host that does not want it takes no ONNX dependency. Without it, a host searches by words as today.
5. **Measured, at no cost.** The runner's `longmemeval --retrieval --embeddings <folder>` plays the search alone, with the model, on the same questions.

## Platform check

1. **Generic:** yes. The model reads any text in any language.
2. **Sensitivity and privacy:**
   - the model runs on the machine that holds the memory, and nothing is sent anywhere;
   - private events are found only when asked, as today;
   - the only network call is the model's download, which carries no personal data.

## Acceptance criteria

1. Given the model, when a text is embedded, then its tokens are those of the reference computed with the model's own tokenizer (`tokenizers`) and ONNX Runtime in Python, and its vector's cosine with the reference's is above 0.999.
2. Given a memory with a model, when what was said is searched with other words than those used ("plants" for "I bought a fern"), then it is found; without a model, the same search finds what BM25 finds today.
3. Given an event forgotten, then its vector is gone too, on every store.
4. Given `filum-mcp` with no model on disk, when it starts, then it answers at once and searches by words, and the model is downloaded in the background and used once it is there; `FILUM_EMBEDDINGS=off` downloads nothing.
5. Given LongMemEval `S` (the 470 questions), when the search alone is measured with the model, then the share of the answer's sessions found in the first 10 is higher than 86.7%, and recall@5 is not lower than 93.8%.

## Open questions

None: the owner delegated the decisions of phase E. The model and the host's way of shipping it are asked in the questions file, and can still change.

## Result (2026-10-04)

The search alone on the 470 non-abstention questions of LongMemEval `S`, at no cost (`evals/reports/retrieval-20261004-083945-words/` and `…-110056-hybrid/`):

| | Words (BM25) | Words and meaning |
|---|---|---|
| Recall@5, a session holding the answer | 93.8% | **96.6%** |
| The answer's sessions found in the first 10 | 86.7% | **91.9%** |
| … multi-session | 82.0% | **89.9%** |
| … temporal | 81.1% | **87.5%** |
| … extraction | 96.0% | 98.0% |
| … knowledge update | 98.6% | 98.6% |

- Making the vectors of a 500-message history takes about 13 seconds on a laptop's CPU (about 25 ms a message).
- The published `filum-mcp` (single file, win-x64) downloads the model on first start and finds "I bought a fern" for "plants".
