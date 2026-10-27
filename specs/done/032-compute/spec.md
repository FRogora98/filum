# Compute: the agent's own code, in an isolated interpreter

> Status: Implemented (2026-10-04). Reviewed (the owner, 2026-10-04: "ambiente isolato va bene, é una feature che dobbiamo per forza fare"; phase E delegated). The interpreter (Jint) is a new dependency, asked to the owner in the questions file.
> Area: engine (tools)
> Author/date: federico rogora (drafted by Claude), 2026-10-04
> Capability 5 of `FEATURES.md`; the half of the roadmap's 026 that spec 026 left out.

## Problem

`collection_aggregate` and `collection_trend` answer fixed kinds of questions. Anything else falls back to the model's own arithmetic, which is a guess presented as a result:
- a simulation ("if I save 15% more a month, when do I reach the goal?");
- a comparison across two collections;
- a what-if, or a custom grouping.

## Goal

The agent writes a short program for the question and runs it in an isolated interpreter, with read-only access to the collections it names. The answer's numbers come from the run, and the code is there to inspect.

## Non-goals

- **No container, no process per person, no network, no files:** an interpreter inside the engine's process, with nothing reachable but the data given to it.
- **No writes from code:** a run returns values. Changes to the memory go through the tools and their checks.
- **No saved programs yet:** turning a run into a procedure or an artifact (capability 10) is later.

## Desired behavior

1. **The tool:** `compute_run(code, collections?, includePrivate?)`.
   - `code` is the body of a JavaScript function, run in strict mode. It returns a value, and `print(…)` adds lines to an output.
   - `collections` lists collection paths. Each is given to the code as `data["/path.csv"]`, an array of rows, one object per row, field → value. A value that reads as a number is a number; everything else is text.
   - Private collections are given only with `includePrivate`, as for searches.
2. **Isolation and limits.**
   - The interpreter has no access to .NET, the network, the disk or the clock beyond `Date`.
   - It stops at 2 seconds, 64 MB, one million statements and a recursion depth of 100.
   - At most 20,000 rows are given.
   - The result is at most 8 KB as JSON, and the output at most 4 KB.
   - Past any limit, the run fails with the reason, and nothing else happens.
3. **What the agent and the person see.**
   - The model gets the result as JSON and the output.
   - The turn's step says which collections and how many rows were used, and carries the code (in its `data`), so the app can show what ran.
   - A run that fails gives the model the error, without a stack trace.
4. **The instructions:** a question that needs processing beyond a total or a trend gets a program run with `compute_run`. Its numbers come from the run, and the answer states the assumptions.

## Platform check

1. **Generic:** yes. Code over the person's own collections, in any domain.
2. **Sensitivity and isolation:**
   - private collections are given only when asked;
   - the code reaches nothing but the rows given to it, and writes nothing.

## Acceptance criteria

1. Given two collections, when code joins them and returns a number, then the number is the right one, and the step names both collections and the rows used.
2. Given code that loops forever, allocates without end, or recurses too deep, then the run stops at its limit with the reason, within about the time limit.
3. Given code that reaches for .NET, `require`, `fetch` or `import`, then it fails: none of them exists.
4. Given a private collection, when it is named without `includePrivate`, then the run is refused; with it, it runs.
5. Given a result over 8 KB, then the run fails and says to return less.
