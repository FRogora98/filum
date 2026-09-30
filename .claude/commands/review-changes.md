---
description: Review uncommitted changes or recent commits against the specs and the project rules
argument-hint: [commit range, e.g. HEAD~3..HEAD — default: uncommitted changes]
---

Review these changes: $ARGUMENTS

If no range is given, review the uncommitted changes (`git diff HEAD` plus untracked files). With a range, review `git diff <range>` and its commit messages. This is a read-only review: **do not edit any file**.

## 1. Understand

- Read `CLAUDE.md` and the diff in full.
- If the changes belong to a spec in `specs/`, read its `spec.md`, `plan.md` and `tasks.md`: the review compares the diff with what was agreed.
- For every changed function or contract, read enough of the surrounding code and of its callers to judge it.

## 2. Check

- **Correctness**: bugs, unhandled failure paths, wrong edge cases. Explain the concrete input that breaks it.
- **Spec adherence**: does the diff do what the plan says, no more and no less? Acceptance criteria without a test?
- **Product rule**: any domain hard-wired in code, prompts, tool descriptions or tests? Any personal data, secret or private reference (this repository is public)?
- **Architecture**: state kept outside the store? Sensitivity bypassed by some tool? Anything that sends data off the machine?
- **Tests**: does each behavior change come with a test? Would the tests fail if the change were reverted?
- **Simplicity**: abstractions nobody asked for, duplicated code where something already exists, unrelated changes.
- Run the verification commands in `CLAUDE.md` for the parts touched and report the outcome.

## 3. Report

Findings ranked by severity, each with `file:line`, what is wrong, and the concrete scenario that shows it. Mark each finding as verified (you ran or read it) or suspected. If nothing is wrong, say so plainly: do not invent findings.
