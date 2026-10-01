# The benchmark: LongMemEval on Filum — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied).

## Approach

1. **`src/Filum.Evals/LongMemEval/`:**
   - `Dataset.cs` holds `LmeInstance` (the authors' fields, snake_case), the ability of each question (the five of the paper, `_abs` for abstention), the sessions in date order (the files do not keep that order), and `LmeSubset` (stratified, ranked by SHA-256 of seed and id, so stable everywhere).
   - `Protocol.cs` holds the framing: a session as a new chat, in parts of at most 8,000 characters; the question with its date; the history as one text for the references.
   - `Judge.cs` holds the authors' prompts verbatim, with their MIT notice. A reply counts as yes when it contains "yes", as theirs does.
   - `Systems.cs` holds the systems:
     - `HostLmeSystem` (Filum on any host);
     - `NoMemorySystem` and `NaiveRetrievalSystem` (BM25, k = 5, through a `DirectModel` read from a host's appsettings);
     - `ClaudeLmeSystem` (Claude Code with filum-mcp, with no memory, or with the whole history), with the prompt on stdin.
   - `Run.cs` holds the run with a dollar cap and an agent-session cap, the estimate, and the report (accuracy over repetitions with its spread, per ability, tokens, dollars per question and per correct answer, median seconds, agent model versions).
   - `Cli.cs` is the `longmemeval` mode.
2. **`scripts/longmemeval-download.sh`** fetches the files into `evals/longmemeval/data/` (ignored) and checks the hashes pinned in `evals/longmemeval/SHA256SUMS`.
3. **The arms** are one host each: the sample host, started with the arm's configuration (`Reliability:CheckModel`, `Memory:ExcludedTools`), no package, `Agent:Name` Filum.
4. **Reports** of the runs the paper uses go to `evals/reports/<date>-<what>/`, with the command and the commit.

## Test strategy

| Criterion | Test |
|---|---|
| 1 | the script, run once; hashes pinned |
| 2 | `LongMemEvalTests`: stratified and stable subset |
| 3 | `LongMemEvalTests`: host system against the sample host (new chats, date order, question last, judge prompt per ability) |
| 4 | `LongMemEvalTests`: references' prompts, BM25 |
| 5 | the arms' configuration; a run of each |
| 6, 7 | `LongMemEvalTests`: report and caps |
| 8 | `dotnet test` of `Filum.Evals.Tests` |
