---
description: Implement a planned spec, one task at a time, keeping build and tests green
argument-hint: <spec number, e.g. 002>
---

Implement spec $ARGUMENTS following its `tasks.md`. Work in phases and say which phase you are in.

## 1. Gate

- Open `specs/$ARGUMENTS-*/`. `spec.md` must be `Reviewed` and both `plan.md` and `tasks.md` must exist. Otherwise **stop** and say what is missing (`/plan` comes first).
- Run the verification commands in `CLAUDE.md` for the parts the spec touches, before changing anything. If they already fail, stop and report: do not build on a red baseline.

## 2. Tasks, in order

For each unticked task in `tasks.md`:

1. **Understand**: re-read the task, the Contract changes in `plan.md` and the code it touches.
2. **Change**: do exactly what the task says, the smallest change that works. Follow the conventions of the surrounding code. No drive-by refactoring: mention it instead.
3. **Verify**: run the task's Verify step and the relevant commands from `CLAUDE.md`, and read the output. A task is done only when they pass.
4. Tick the task in `tasks.md`.

If a task turns out to be wrong or impossible, **do not diverge silently**: stop, explain why, propose the fix to `plan.md` (or to `spec.md`, if the problem is in the requirement), and wait for the humans.

## 3. Close

- Run the T-close task: check each acceptance criterion against code and tests and say which test proves it.
- Run all the verification commands in `CLAUDE.md` one last time.

## 4. Report

List the tasks done, the files changed, the commands run with their outcome, and anything you could not verify. Keep verified facts separate from assumptions. Do not commit or push: the humans decide.
