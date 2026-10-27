# Recall anchored in facts

> Status: Implemented (2026-10-18). Reviewed (the owner, 2026-10-18, idea 3 of the state-of-the-art design; the reranker
> approved only off by default and downloaded on request)
> Area: engine (`events_search`: `EventSearch`, `EventVectorService`, `EventTools`, `FactLinks`), embeddings, filum-mcp
> Author/date: federico rogora (drafted by Claude), 2026-10-18

## Problem

On LongMemEval (400 questions, two rounds) Filum's facts memory came out
level with a BM25 baseline that hands the model the five best sessions whole. A reading of its errors says why:
- **What a search hands over.** `events_search` returns at most 8 matching turns (`MemoryOptions.EventsSearchMax`),
  each cut to an excerpt of about 1,000 characters (`EventSearch.Excerpt`), with the reply after it cut to 300
  characters (`EventTools.SearchEvents`). The answer is often a few turns away, or deep in a long reply.
- **Facts are reached only by name.** A fact is shown beside a search only when every word of its subject's name is in
  the query (`FactLinks.About`); "where does my sister live" does not reach `Anna · city: Lyon` unless the query says
  "Anna".
- **A fact does not lead back to where it was said.** A fact keeps the ids of its source events, but a search does not
  show the conversation they belong to; `events_read` (spec 038) was called in 13 of 200 answers.
- **The number of hits is fixed.** Eight turns whether one conversation answers the question or six do.

## Goal

A search of what was said returns the facts that match the question, by words and by meaning, each with the
conversation it came from, and the best conversations, chosen by how far their scores drop, as whole as a character
budget allows; a local reranker can sharpen the order, off unless the person turns it on.

## Non-goals

- No change to `IMemoryService.SearchEventsAsync` or `MemoryEventHit`: the ranking of turns stays the engine's API;
  the tool groups and cuts what it shows.
- No fact vectors in a store and no table change: a fact's vector is derived, made when a search needs it and kept in
  the process.
- No time expansion of the query (a later spec), no new tool, no change to the tool catalog's names or parameters.
- The reranker ships no file: it is downloaded only when the person turns it on.

## Current behavior

- Ranking: BM25 and, with the embedding model, cosine over e5 vectors, fused by reciprocal rank (`EventSearch.Fuse`,
  depth 50), top `EventsSearchMax` turns (`EventVectorService.RankAsync`; by words only in `NullEventVectors`).
- Text: each turn with its date, number, conversation title and an excerpt, then 300 characters of the reply
  (`EventTools.SearchEvents`); facts first when the query names their subject (`FactLinks.About`).
- Structured content: the matching turns (`ToolData.Matches`, spec 040).

## Desired behavior

1. **Facts that match.** Before the conversations, `events_search` shows up to 5 facts that hold now and match the query:
   those whose subject (or an alias, spec 041) the query names, as today, then the others ranked by BM25 over
   "subject attribute value" fused with cosine when the embedding model is loaded. Each line says where it came from
   (`from event #N`) when it has sources.
2. **Their conversations.** The conversation of the first source of the best one or two matching facts comes first
   among the conversations shown, marked as the source of that fact.
3. **Conversations, not turns.** The matching turns are grouped by conversation (an event outside a conversation is
   grouped with the others of its day, as in `events_read`); a conversation scores as its best turn.
4. **Adaptive count.** The conversations shown are the best ones down to the largest drop in score among the first
   `RecallSessionsMax` (default 5), at least one; a fact's conversation of item 2 is always shown.
5. **A budget.** What the search shows is at most `RecallBudgetChars` characters (default 24,000), spread over the
   conversations shown in their order: a conversation that fits its share is shown whole, in order; a longer one shows
   its matching turns whole (or, when a turn alone is longer than its share, an excerpt around the query), each with
   the turn before and the reply after when they fit, and says which event to read for the rest with `events_read`.
   What a conversation leaves of its share goes to the next.
6. **A reranker, off by default.** A host may give the engine an `IReranker` (`UseReranker`): the best
   `RerankDepth` turns (default 20) of the fused ranking are ordered again by its scores before they are grouped. With
   none, the order is the fusion's. A reranker that fails is not used for that search, and the search still answers.
7. **The local reranker.** `Filum.Embeddings` gives `BgeReranker` (bge-reranker-v2-m3, Apache-2.0, the int8 ONNX export,
   about 570 MB, on the CPU, inputs cut to 256 tokens) and `RerankerFiles` (where its files come from, with their
   SHA-256: a download is used only when it matches). Its tokenizer is the e5 model's file.
8. **filum-mcp.** `FILUM_RERANKER=on` downloads the reranker into the models folder in the background and uses it once
   loaded; unset or anything else, nothing is downloaded and nothing changes. Search works by words and meaning while
   it loads, and after a failed download.
9. **Structured content** stays the matching turns (spec 040), in the order shown.

## Platform check

1. Generic: words, meaning and conversations of any domain; no model is called and no domain is named.
2. Sensitivity: private events are shown only when `includePrivate` is asked, as today, and a fact's conversation is
   read with the same rule; the reranker runs on the person's machine and sends nothing anywhere.

## Acceptance criteria

1. Given `Anna · city: Lyon` recorded from event N, when the query is `which city did she move to` (it does not name
   Anna), then the fact is shown, with `from event #N`.
2. Given a fact whose first source is in conversation C, when a search matches the fact, then C's messages are shown
   first, marked as the source of the fact, even when C's turns do not match the query.
3. Given three conversations whose best turns score far above the rest, when searched, then those three are shown and
   no other (the drop), and never more than `RecallSessionsMax`.
4. Given a conversation shorter than its share of the budget, when it matches, then all its messages are shown in order;
   given a longer one, then its matching turns are shown whole with their neighbours and a pointer to `events_read`.
5. Given any search, then the text shown is at most `RecallBudgetChars` plus the lines that name dates and events.
6. Given a fake reranker that reverses the order, when searched, then the turns come in its order; given one that
   throws, then the search answers in the fusion's order.
7. Given no reranker and no embedding model, then a search answers by words, as before, with items 1-5.
8. `RerankerFiles` names each file with a URL at a fixed revision and a SHA-256, and a file whose hash does not match is
   not kept (a test with a local fake server, as for the e5 model's files).
9. Given filum-mcp without `FILUM_RERANKER`, then no reranker is registered and nothing is downloaded.
10. The tool catalog's snapshot does not change.

## Measurement (before it counts)

A pre-registered round, written before any run: LongMemEval_S questions not
used in those two rounds, the facts arm with this spec against the facts arm on the engine just before this spec and against BM25 top-5
sessions, the same answer model (Haiku, on subscription quota, one question at a time with `--stop-on-limit`), and its
thresholds and test (exact McNemar) fixed in advance. The reranker is measured as its own arm only if the round without
it is run first.

## Open questions

None: the owner set the scope (fact-anchored recall, adaptive budget, the source conversation within the budget,
the reranker off by default and downloaded on request) and the technical choices are the engine's.
