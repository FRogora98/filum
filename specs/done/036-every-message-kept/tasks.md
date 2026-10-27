# Every message kept, whatever the model — tasks

> A small change: the plan is in the spec's Desired behavior.

- [x] **T1 — `filum-mcp log`**
    - Do: `HookLog` (read a hook's JSON or plain text, parts of 8,000 characters, one conversation per session, nothing on stdout, exit 0).
    - Verify: `HookLogTests` (criteria 1 to 3), with the real process.

- [x] **T2 — `--hook` in the evals**
    - Do: `McpScenarioRunner.WriteConfigAsync` writes the hook's settings beside the MCP config; `ClaudeCodeAgent` passes them with `--settings`; `--hook` in `longmemeval` and `mcp`.
    - Verify: `LongMemEvalTests.With_the_hook_the_agent_gets_settings_whose_hook_logs_into_the_runs_memory` (criterion 4). By hand on 2026-10-06, the question that failed without the hook (`50635ada`, Haiku 4.5): correct with it, and the log holds both history sessions.

- [x] **T3 — README**
    - Do: the hook for Claude Code, under Install.

- [x] **T-close**
    - Do: LongMemEval `oracle` 20 with `--hook --consolidate` against the run without the hook (9 of 19); results into the spec; move it to `done/`.
