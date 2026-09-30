# filum-mcp — tasks

> Rules: tasks are small (each leaves the build and the tests green), ordered (later tasks may depend on earlier ones), and each states how to verify it. Tick them off as you go. If a task turns out to be wrong, fix plan.md or spec.md first, then the task: never diverge silently.

- [x] **T1 — The server**
    - Do: `src/Filum.Mcp` (store, per-call tools, refusals as errors, instructions, stderr logging, exit on a bad folder); packages; solution.
    - Verify: `dotnet build`; `dotnet run --project src/Filum.Mcp` answers an initialize request typed on stdin.

- [x] **T2 — End-to-end tests**
    - Do: `tests/Filum.Mcp.Tests` for criteria 1–7.
    - Verify: `dotnet test Filum.slnx` green.

- [x] **T3 — Release**
    - Do: `scripts/smoke-mcp.sh`, `.github/workflows/release.yml`; README install and `CLAUDE.md`.
    - Verify: the smoke script passes locally on a `win-x64` single-file build; secret scan and private-name check clean; push; tag `v0.1.0` (the owner's yes); the release shows the five files (criterion 8).

- [x] **T4 — By hand in Claude Code**
    - Do: download the Windows file from the release, register it with `FILUM_HOME` on a temporary folder, save a fact in one session and ask for it in a new one.
    - Verify: criterion 9; the outcome is written here.
    - Outcome (2026-09-30, `claude -p` with Haiku, `--strict-mcp-config`, `--tools ""` so only Filum's tools were there, a temporary `FILUM_HOME`, each prompt a new session):
      - a neutral fact saved in one session was found in the next;
      - found along the way:
        - **Claude Code's own auto-memory can answer instead of Filum** when its file tools are on: the first run "passed" from `~/.claude/projects/…/memory`, with nothing in Filum's folder. The check is only valid with the host's own tools off.
        - **The host's habits leak in:** Haiku wrote a `MEMORY.md` index into Filum's folder. The instructions now say the core's map is the only index.
        - **A fact the model marked private was not found** by a direct question, because the search skipped private files. The instructions now say that a direct question about a specific thing of theirs is a request for private content.
      - after both changes (released as v0.1.1): the private fact and the neutral fact are found in new sessions, the map is in `/filum.md`, and there is no extra index file.

- [x] **T-close — close the spec** (always the last task)
    - Do: check every acceptance criterion; set spec.md and spec.it.md to Implemented; move the folder to `specs/done/`; update `specs/README.md`; update the submodule in the private repository and its roadmap.
    - Verify: `dotnet build`, `dotnet test`, `bash scripts/check-secrets.sh` pass here; `dotnet test` passes in the private repository.
