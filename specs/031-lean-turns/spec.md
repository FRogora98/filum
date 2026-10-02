# Lean turns: changing facts read right, at less cost per message

> Status: Reviewed (the owner delegated the specs after 030, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (tools, platform instructions) / the turn
> Author/date: federico rogora (drafted by Claude), 2026-10-02
> Roadmap: phase E, after 030; from its measurement (`evals/reports/20261002-170349/`)

## Problem

The measurement of spec 030 left two gaps.

- **Changing facts.** Gemma answered 2 of 4 knowledge-update questions of `oracle`.
  - "Where did I initially keep my old sneakers?" was answered with the later place.
  - A collection that grew by one ("I just added a new coin", 37 before) was answered with a row count instead of 38.

  In both, the agent answered from what it found first, without checking how the value changed: neither `facts_history` nor what was said.
- **Cost per message.**
  - A turn sends about 19,000 input tokens. The fixed part is resent at every model call of the turn, and a turn makes three or four: the 26 tools' descriptions and schemas (about 14,500 characters, 3,600 tokens) and the platform instructions (about 1,100 tokens).
  - Prompt caching does not help today: through OpenRouter these models are served by several providers, none reported a cached token on a repeated prefix (probe of 2026-10-02).
  - The catalog's prices are below what some providers charge (Gemma: 0.09 against 0.15 USD per million input tokens at one of them), so the reported cost is optimistic.

## Goal

The agent reads changing facts the right way, and a turn sends less, with no capability lost.

## Non-goals

- **No change to what the memory keeps** (spec 030) or to the tools' behavior: only their descriptions, the instructions, and which tools a host offers.
- **No provider pinning or caching work:** a host can configure its providers; this spec measures, it does not route.
- **No cost as charged:** recording what a provider reports instead of the catalog's estimate is left for later; reports keep saying the cost is estimated.
- **No per-question tuning:** every rule is generic, written for any person and any domain, never for a benchmark's wording.

## Current behavior

- `PlatformInstructions.Text` says when to record facts, not how to answer about them.
- The tools' descriptions (`MemoryTools`) are written for a model with no other guidance, and repeat what the instructions say.
- The hosted turn offers `memory_overview`, although its instructions already hold the core, the map and the skills.

## Desired behavior

1. **Reading what changes.** The instructions tell the agent, generically:
   - a question about what holds now, what held before or at first, since when, or how many is answered from `facts_current` / `facts_history`, checked against `events_search` when the facts may be missing;
   - when the person says a value changed by an amount ("one more", "two fewer"), the new value is worked out from the last one and recorded with `fact_record`.
2. **Shorter tools.** Every tool keeps its name, parameters and behavior. Its description and parameter descriptions say what it does and nothing the instructions already say. The whole catalog is at most 10,000 characters as the model receives it (from about 14,500; about 6,500 of them are the schemas' structure, which no wording shortens).
3. **No tool the turn does not need.** The hosted turn does not offer `memory_overview`, whose content is already in the instructions. `filum-mcp` keeps it.

## Platform check

1. **Generic:** yes. The rules speak of values that change, never of a domain.
2. **Sensitivity:** unchanged. No tool loses its sensitivity rules, and the public repository gains nothing personal.

## Acceptance criteria

1. Given the catalog, when it is serialized as the model receives it, then it is at most 10,000 characters, every tool has its name, parameters and a description, and the snapshot shows the change.
2. Given a hosted turn, when the tools are offered, then `memory_overview` is not among them; in `filum-mcp` it is.
3. Given the instructions, then they hold the rules of desired behavior 1, and the eval scenarios of the engine stay green on the default model.
4. Given LongMemEval `oracle` (the 20 questions of spec 030) on Gemma 4 31B, when it is played again, then knowledge updates are at least 3 of 4, accuracy is not lower than 85%, and the input tokens per question are lower than in spec 030's report.

## Open questions

None: the owner delegated the decisions. The measurement (criteria 3 and 4) costs about $0.60 and needs the owner's yes before it runs.
