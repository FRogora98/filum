---
description: Fix a bug with Reproduce → Fix → Verify → Judge (failing test first)
argument-hint: <bug description, or path to a note/transcript describing it>
---

Fix this bug: $ARGUMENTS

If the input is a path, read the file. Work in these phases, in order, and say which phase you are in.

## 1. Reproduce (no production code changes)

- Read `CLAUDE.md` and the code of the area involved. Trace the real flow end to end.
- Write a test that asserts the **observable symptom** from the bug description (what the user sees), not the mechanism you suspect. Put it next to the existing tests of that area.
- Run it and confirm it is **red for the right reason**: the failure message must match the symptom, not a setup error or a typo.
- If you cannot reproduce it, **stop**: report what you tried and what is missing (steps, data, environment). Do not fix what you have not reproduced.

## 2. Fix

- Find the **root cause**; the bug title is a hint, not the cause. Never hide the symptom behind a guard or a try/catch.
- Before changing shared code, find every caller and check the change is safe for all of them.
- Make the smallest production-code change that fixes the cause. **Do not edit the reproduce test**: it is the oracle. If you believe the test is wrong, go back to phase 1 and say why.

## 3. Verify

- Run the reproduce test: it must be green.
- Run all the verification commands in `CLAUDE.md` for the parts you touched: nothing else may break.

## 4. Judge (be your own reviewer)

Answer each question honestly, with evidence (`file:line` or command output):

- Is the test a genuine oracle of the symptom, or does it just mirror the fix?
- Does the fix address the root cause, or does it suppress a code path or game the test?
- Would the diff survive a code review: minimal, in the style of the code around it, no unrelated changes?

If any answer is no, go back to the phase that caused it.

## 5. Report

The root cause in one or two sentences, the files changed, the test that proves the fix, the commands run with their outcome. Do not commit or push: the humans decide.
