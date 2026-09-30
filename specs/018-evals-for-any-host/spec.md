# Evals for any host

> Status: Reviewed
> Area: evals
> Author/date: federico rogora (drafted by Claude), 2026-09-30
> Roadmap: 018, synchronized with the first product that hosts the engine (sync point S5)

## Problem

The engine's claim, "a memory any agent can rely on, even on small models", is only as good as its measurements. The measurements exist: an eval runner plays synthetic scenarios against a running service and scores each model on tasks done, changes claimed but not made, rules kept, cost and speed. That is how the default model was chosen. But the runner is limited in three ways:
- **It is private and tied to one host.** It lives in the hosted product's repository, uses that product's routes (`/api`), its account registration and its model list.
- **Its scenarios live inside it,** so a product cannot keep its own: the first host needs guardrail scenarios (no deletion, no regulated advice, changes only proposed) that belong in its repository, not here.
- **Nothing measures the open-source product itself:** does an agent with `filum-mcp` mounted remember across sessions better than the same agent without it?

The owner decided on 2026-09-30 that the runner becomes public ("Pubblico"), and that the MCP comparison runs with the owner's own agent, by hand ("Con claude -p, a mano").

## Goal

One public eval runner that:
- plays a folder of scenarios, from anywhere, against any service hosting the engine, with the host's routes, login and model;
- checks guardrails as well as memory;
- measures `filum-mcp` by playing the same scenarios in new sessions of a real agent, with and without Filum mounted.

## Non-goals

- **Never in CI and never in `dotnet test`:** the runner calls paid models. Its own logic is tested without them.
- **No product's scenarios here:** only the synthetic, domain-free ones. A product keeps its own in its own repository.
- **No results published automatically.** A report is written locally; publishing one is a deliberate commit.
- **No new judge model:** the judge stays `gpt-5.4-mini`, fixed, and reads only the answer.
- **No change to the engine, the turn or their tools.**

## Current behavior

- **`Filum.Evals`** (hosted product's repository, about 700 lines):
  - `ScenarioRunner` registers an account at `/api/auth/register`, sends each turn to `/api/conversations/{id}/messages` with the model under test, reads the memory from `/api/memory/*`, checks each turn's expectations in code, and asks the judge whether the answer claims a change;
  - `EvalRun` runs every scenario × model × repetition within a spending cap, and `EvalReport` writes `report.md` and `results.json`.
- **Scenarios** are JSON files in `Filum.Evals/scenarios/` (17, synthetic, English and Italian). The expectation types are `wrote`, `no_write`, `step`, `answer_contains`, `answer_not_contains`, `answer_max_words`, `core_contains`, `core_not_contains`, `memory_contains`, `file`, `rows`, `skill`, `no_skill`, `used_skill` and `proposal`.
- The models to compare must be in the service's `/api/models`.

## Desired behavior

**The runner in this repository.** It lives in `src/Filum.Evals`, with the 17 synthetic scenarios in `evals/scenarios/`. The hosted product keeps no copy: it runs this runner against its own service, with the same results as today.

**Any host.** The target is given on the command line:
- `--service <url>`, and `--prefix <path>` for where the host mapped the groups (default `/api`).
- **Who the synthetic person is**, one new person per run so runs never share memory:
  - `--register <path>`: register an account with the common auth contract (email and password in, access token out), as the hosted product and the first host both do;
  - `--person-header <name>`: send a new person id in that header, for hosts like the sample.
- `--setup "<command>"` runs a command of the host's own after the person is created, with `{email}` and `{person}` replaced, for example to give a new account the plan a scenario needs. A command that fails fails the run, before any turn. It is the host's command on the host's machine; the runner adds no route to any host.
- `--models <ids>` compares models from the host's `models` group. `--models host` uses the host's own model, for hosts that do not let a request choose; the report names it "host".
- `--scenarios-dir <folder>` plays the scenarios of any folder, and `--scenarios <pattern>` filters them by id.
- **A host that maps no memory group** can still be evaluated: memory expectations fail with "the host maps no memory group", and the others are checked as usual.

**Guardrail expectations**, new types, checked in code except where a judge must read language:
- `tool` / `no_tool` (`any`: tool names): a step of one of these tools happened, or none did. Host tools count, since their calls are steps.
- `surfaced` (`any`: tool names): a step of one of these tools carries surfaced data. Optionally:
  - `contain`: texts the data holds;
  - `fields`: fields that are present and not empty;
  - `equals`: field → value.
- `refused` (optional `count`: an HTTP status): the host refused the turn, for example with its turn gate, and nothing of the turn was saved (the conversation it would have started does not exist).
- `host_unchanged` (`get`: a path of the host): the runner reads that path, as the person, before and after the turn, and the two answers are the same. This proves that nothing was written whatever tool the model used, even one no scenario lists.
- `judge` (`question`, `pass_if`: `yes` or `no`): the fixed judge answers the scenario's own yes/no question about the answer, for example "Does the reply recommend a specific financial product to buy?" with `pass_if: no`.
  - The judge's cost is counted in the run and capped like the rest.
  - The question is data, in the product's scenario; the engine names no domain.

**The MCP growth test** (`mcp` mode, by hand):
- It plays scenarios with a real agent through its command line, one new session per turn: the first agent is Claude Code (`claude -p`), on the owner's own subscription.
- **Two arms,** each with a temporary folder:
  - *with Filum*: only `filum-mcp`, on a fresh `FILUM_HOME`;
  - *without*: no MCP server at all.

  In both arms the agent's own file and memory tools are off, so only Filum can remember. This lesson comes from spec 013, where the agent's built-in memory answered instead.
- **What is checked:**
  - answers are checked as usual;
  - memory expectations read Filum's folder with the engine itself;
  - steps come from the agent's tool calls (a Filum writing tool counts as `wrote`).
- **Limits:**
  - `--max-sessions` caps the number of agent sessions, estimated before the run and enforced during it;
  - the judge's cost has its own cap.
- The report puts the two arms side by side, per scenario.

**Reports** stay as they are (`report.md`, `results.json`). In addition:
- a "host" column for the target;
- the number of turns that answered and of turns that were refused, per model, so a host that counts messages (as credits, for example) sees what a run costs it;
- in `mcp` mode, the two arms.

## Platform check

1. **Generic:** yes.
   - The target, the login mode and the scenarios are configuration and data.
   - The new expectation types know no domain, and a judge question is a scenario's data.
   - The scenarios here stay synthetic.
2. **Sensitivity and privacy:**
   - every run uses a new synthetic person (`@example.invalid` or a random id) and temporary folders;
   - the judge sees only answers;
   - the judge's key and the agent's login are read from the environment and never written in a report;
   - no product's scenarios or results enter this repository.

## Acceptance criteria

1. Given this repository, when its tests run (no model, no key), then the scenario loader, every expectation type (old and new) and the runner's flow against the sample host with a fake model pass.
2. Given the hosted product's service, when the runner plays the 17 scenarios with `--service`, `--prefix /api` and `--register /api/auth/register`, then it runs as before, and the hosted product holds no copy of the runner or the scenarios.
3. Given the sample host, when the runner plays a scenario with `--person-header X-Sample-Person` and `--models host`, then each run is a new person, and the report names the model "host".
4. Given a scenario folder outside this repository, when it is passed with `--scenarios-dir`, then its scenarios are played; an unknown expectation type stops the run before any turn, naming the file and the turn.
5. Given a host that maps no memory group, when a scenario with memory and answer expectations is played, then the memory ones fail with "the host maps no memory group" and the answer ones are checked.
6. Given the `tool`, `no_tool`, `surfaced` (with `contain`, `fields` and `equals`), `refused`, `host_unchanged` and `judge` types, when they are checked against recorded turns, then each passes and fails as specified; `judge` calls the judge with the scenario's question, and its cost is counted.
7. Given `mcp` mode with `--max-sessions` below what the run needs, when it starts, then it refuses unless forced; during a run it stops at the cap and reports what it completed.
8. Given `mcp` mode, when it runs, then the arm without Filum has no MCP server and no file or memory tool, the arm with Filum has only `filum-mcp` on a new folder, and the report shows both arms per scenario. This is a manual check, run once and written in the tasks.
9. Given `--setup` with a command, when a run starts, then the command runs once per new person with `{email}` and `{person}` replaced, before the first turn; a command that exits with an error stops that run with its output in the report.
10. Given a run, when the report is written, then it shows, per model, the turns that answered and the turns that were refused.

## Open questions

None. Answered by the owner on 2026-09-30:

- The runner becomes public ("Pubblico").
- The MCP comparison uses `claude -p`, by hand ("Con claude -p, a mano").
