# Trends: forecasts and what stands out, on any collection over time

> Status: Implemented (2026-10-04). Reviewed (the owner delegated the specs of phase E, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (tools)
> Author/date: federico rogora (drafted by Claude), 2026-10-04
> Roadmap: phase E, spec 026 ("a forecast with its error on synthetic series; an anomaly found and explained"). The roadmap's other half of 026, sandboxed computation (capability 5), is left out and asked to the owner.

## Problem

A person who keeps a collection with a date and a number gets exact totals from `collection_aggregate`. But a question like "where is this heading?" or "what was unusual?" is answered by the model's own arithmetic, which is a guess presented as a result.

## Goal

The agent answers where a value is heading and what stands out, from computation: a forecast with the range it is likely in and the error the method had on the series' own past, and the periods far from the trend, each with what was expected. Always said to be estimates.

## Non-goals

- **No sandbox and no code written by the agent** (capability 5): asked to the owner.
- **No model and no new dependency:** plain code, deterministic. ML.NET was approved but is not needed for this.
- **No seasonality:** the forecast is a level and a trend. Seasonal series are later, when a person's data asks for it.

## Desired behavior

1. **One tool, `collection_trend(path, dateColumn, column?, operation = sum, period = month, horizon = 3)`.**
   - The series is the collection grouped by day, week, month or year of the date field, exactly as `collection_aggregate` groups it.
   - A sum or a count of a period with no rows is zero; an average leaves the period out.
2. **The forecast.**
   - Holt's linear smoothing: its two weights are chosen on the series' one-step errors.
   - Each forecast period has an estimate and a range of about 95% (1.96 times the one-step error, growing with the distance).
   - It needs at least 6 periods; with fewer, the tool says so and forecasts nothing.
3. **Its error, on its own past.** The last periods (a quarter of the series, at least one, at most the horizon) are forecast from the ones before them. The mean error is given, beside the error of repeating the last value.
4. **What stands out.** From 8 periods on:
   - each period's one-step error is scored by a robust z-score (median and median absolute deviation);
   - beyond 3.5, the period stands out, with its value, what the trend expected, and how many usual deviations apart they are.
5. **The instructions:** where a value is heading, and what stands out, come from `collection_trend`, always called an estimate with its range.

## Platform check

1. **Generic:** yes. Any collection with a date and a number, in any domain.
2. **Sensitivity:** it reads a collection like `collection_aggregate`, with the same rules, and writes nothing.

## Acceptance criteria

1. Given a steady monthly rise, when its trend is computed, then the next periods are forecast along the line, within a range, and the error on its own past is small and smaller than repeating the last value.
2. Given a series with one period far from the others, then that period stands out, with what the trend expected there.
3. Given a series with a month with no rows, then the month counts as zero; given fewer than 6 periods, then nothing is forecast and the tool says why.
4. Given the tool in a turn, then its answer calls its numbers estimates and names what stands out, and its step says what was computed.

## Open questions

None: the owner delegated the decisions of phase E. The sandbox is asked in the questions file.
