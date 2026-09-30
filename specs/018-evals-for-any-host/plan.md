# Evals for any host — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied).

## Approach

1. **Move `Filum.Evals`** to `src/Filum.Evals`:
   - the scenarios go to `evals/scenarios/`, copied to the output as today;
   - the DTOs come from `Filum.Agent.Http`, and the two auth DTOs of the common contract are declared here (`RegisterRequest(Email, Password)`, reading `accessToken` from the answer);
   - `.env` lookup keeps "environment first, then the nearest `.env`".

   The hosted product removes its copy, runs this one, and keeps its runner test against its own service (the `--register` path).
2. **`HostTarget`** (`Prefix`, `Register` or `PersonHeader`, `Setup`) is given to `ScenarioRunner`. A `Person` created per run carries its auth (a bearer token, or a header with a new id).
   - `--setup` runs through the system shell (`cmd /c` on Windows, `sh -c` elsewhere), with `{email}` and `{person}` replaced; a non-zero exit fails the run with the output.
   - `--models host` sends no model, and the run's model is "host"; without the `models` group the estimate is skipped, which is said, and the cap still stops the run.
   - A `404` from the memory group gives a `MemorySnapshot` marked unavailable, and the memory expectation types fail with "the host maps no memory group".
3. **Expectations:**
   - `tool`, `no_tool`, `surfaced` (`contain`, `fields`, `equals`), `refused` (status, plus "the conversation does not exist") and `host_unchanged` are checked in code.
   - The runner collects what the code needs: the status of a refused turn, whether its conversation exists afterwards, and the `get` answers before and after the turn.
   - `judge` is checked by the runner with `IClaimJudge.AskAsync(question, answer)`, whose cost goes to the judge's cost.
4. **Report:** `ModelScore` gains `Answered` and `Refused`; the report's first line names the target.
5. **`mcp` mode** (`filum-evals mcp …`):
   - `McpScenarioRunner` plays each turn as a new `claude -p` session in a temporary folder, with `--output-format stream-json --verbose` and `--tools ""`. The arm *with* has `--strict-mcp-config --mcp-config <filum only>`, the arm *without* `--strict-mcp-config` and an empty config.
   - Tool uses come from the stream (`mcp__filum__<tool>`) and become steps: `wrote` for the engine's writing tools, `read` otherwise.
   - The memory is read from `FILUM_HOME` with `LocalFolderStore` and `MemoryService`.
   - `filum-mcp` is the build next to the runner (a project reference), started with `dotnet`.
   - `--max-sessions` is checked against turns × reps × 2 before the run and counted during it.
6. **Tests:**
   - `tests/Filum.Evals.Tests` holds the expectation tests (moved, plus the new types) and the runner against the sample host with the fake model, linking the sample host test infrastructure of `Filum.Agent.Tests`;
   - the `mcp` stream parsing is tested on a recorded stream.

Alternatives discarded:
- **A test endpoint on hosts to give plans:** the first host rejects it (no route that could grant a plan in production).
- **Name lists only for "no write":** they miss a tool nobody listed.

## Constraints honored

- **Never in CI and never in `dotnet test`:** only logic tests run there; the CLI is run by hand.
- **Synthetic people and temporary folders only.**
- **Keys and logins stay in the environment:** they are never written to reports.

## Contract changes

- **The CLI:**
  - options `--prefix`, `--register`, `--person-header`, `--setup`, `--scenarios-dir`, `--models host`;
  - the `mcp` mode with `--max-sessions`, `--agent-model` and `--force`.
- **Scenario format:**
  - new types `tool`, `no_tool`, `surfaced`, `refused`, `host_unchanged` and `judge`;
  - new fields `fields`, `equals`, `get`, `question` and `pass_if`.
- **Report:** answered and refused turns per model.

## Files touched

| Where | Change |
|---|---|
| public `src/Filum.Evals/**`, `evals/scenarios/*.json` | moved, then extended |
| public `tests/Filum.Evals.Tests/**` | new |
| public `Filum.slnx`, README, `CLAUDE.md` | projects, docs |
| private `Filum.Evals/**` | removed |
| private `Filum.AgentService.IntegrationTests/EvalExpectationTests.cs` | removed (moved) |
| private `Filum.AgentService.IntegrationTests/EvalRunnerTests.cs` | the `--register` target |
| private `CLAUDE.md` | the eval command |

## Risks and mitigations

- **`claude -p` output changes between versions.** Parsing is limited to `tool_use` names and the final `result` text, and a recorded stream is tested.
- **An agent session costs the owner's subscription.** `--max-sessions`, estimated first; by hand only.

## Test strategy

| Criterion | Test |
|---|---|
| 1 | `dotnet test` of `Filum.Evals.Tests` |
| 2 | private runner test with `--register`; no `Filum.Evals` left in private |
| 3 | runner against the sample host with a person header and model "host" |
| 4 | loader on a temporary folder; an unknown type names the file and the turn |
| 5 | sample host without the memory group (a test host setting): memory checks fail with the message |
| 6 | expectation tests for each new type; `judge` with a scripted judge |
| 7 | `mcp` estimate against `--max-sessions`; cap stop with a fake agent command |
| 8 | by hand: one scenario, both arms, report written in the tasks |
| 9 | `--setup` with a command that echoes and one that fails |
| 10 | report shows the answered and refused counts |
