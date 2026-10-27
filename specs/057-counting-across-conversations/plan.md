# Plan: counting across conversations

Spec: [spec.md](spec.md).

## Contracts

- `MemoryOptions.EventsListMax` (100) and `EventsListChars` (16,000), in `Filum.Engine.Abstractions`.
- `IMemoryEvents.ListEventsAsync(userId, query, from, before, includePrivate, max)` and the same on `IMemoryService`:
  the spoken events that mention any of the query's alternatives (`|`), plus the search's own best matches, in day
  order, at most `max`. `PublicAPI.Unshipped.txt` gains them.
- `events_search` gains `every` (bool, default false): the tool catalog snapshot changes by that parameter only.

## Code

1. `EventSearch.Mentions(text, alternative)`: every word of the alternative (3 letters or more) starts a word of the
   text (so a plural matches); `EventSearch.Around(text, query, length)`: an excerpt centred on the first match.
2. `EventService.ListAsync`: the alternatives, the spoken events in the window, those that mention one, plus the hits
   of the normal search (words and meaning) for the whole query; ordered by day, then id; at most `max`.
3. `EventTools.SearchEvents`: with `every`, one line per event (day, number, who, excerpt of about 160 characters),
   within `EventsListChars`, and a closing line with the count and whether a cap was reached; structured content as
   the matches. Without `every`, a search whose hits reached `EventsSearchMax` ends with the hint to list.
4. `McpInstructions`: one short line on counting (no tool outside the core profile named, so the line stays in every
   profile); the details in the `every` parameter's description. Room made under 4,096 by shortening other lines.

## Measure

5. Development on round `5c-20261017` (200 questions) only; then the frozen test set, once, two arms. Pre-registered in
   the owner's evaluation notes before any test run.
