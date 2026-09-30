---
description: Turn a reviewed spec into plan.md (contracts first) and tasks.md
argument-hint: <spec number, e.g. 002>
---

Write the plan and the tasks for spec $ARGUMENTS. Work in phases and say which phase you are in.

## 1. Gate

- Open `specs/$ARGUMENTS-*/spec.md`. If the status is not `Reviewed`, or the Open questions section still has unanswered items, **stop**: list what is missing and ask the humans. Do not plan on a Draft.

## 2. Understand (read only)

- Read `CLAUDE.md`, `docs/VISION.md` (the principles and guarantees the design must honor), the spec, and `specs/_template/plan.md` and `tasks.md`.
- Read all the code the spec touches and find every caller of the shared code you will change (the engine, stores, tools, descriptions).
- If the code contradicts what the spec assumes, stop and say so: the spec must be fixed first.

## 3. Plan

Write `specs/NNN-name/plan.md` from the template:

- **Approach**: the chosen design; one line for each discarded alternative and why it lost.
- **Constraints honored**: how the design respects the project rules (generic in code, one person per memory, the person's files stay readable, determinism, nothing leaves the machine).
- **Contract changes**: written before any code, as they will look once built (tools and their parameters, file formats on disk, configuration, command line). This is the heart of "spec first".
- **Files touched**, **Risks and mitigations** (including how to turn it off or revert it), **Test strategy** (which acceptance criterion is proven by which test).

Then write `specs/NNN-name/tasks.md`:

- Small tasks, in order, each leaving build and tests green, each with a concrete **Verify** step.
- Every acceptance criterion is covered by at least one task.
- The last task is always **T-close** from the template.

Keep it the smallest design that satisfies the acceptance criteria: no abstraction nobody asked for.

## 4. Report

Summarize the approach in a few lines, list the tasks, and point out the riskiest step. Do not start implementing and do not commit: the humans read the plan first.
