---
description: Turn an idea, a note or a meeting transcript into spec drafts (spec.md + spec.it.md)
argument-hint: <idea text, or path to a transcript/notes file>
---

Draft one or more feature specs from this input: $ARGUMENTS

If the input is a path, read the file. Work in phases and say which phase you are in.

## 1. Understand (read only)

- Read `CLAUDE.md`, `docs/VISION.md`, `specs/README.md` and the templates in `specs/_template/`. Map each feature to what `docs/VISION.md` says Filum is for; a feature that serves none of it is a signal to question it.
- List the existing specs (`specs/*/` and `specs/done/*/`) so you do not duplicate one.
- Read the code of the area the idea touches, so "Current behavior" describes what really runs.
- Extract the **distinct features** in the input. A meeting usually mixes several topics: one feature = one spec. Ignore chatter, keep decisions, requirements, complaints and open doubts.
- Show the list of features you found (one line each) and which existing spec, if any, each one overlaps. If a feature only updates an existing Draft spec, say so: update that spec instead of creating a new one.

## 2. Draft

For each feature:

- Pick the next free number (three digits, never reused) and a short kebab-case name: `specs/NNN-name/`.
- Copy `specs/_template/spec.md` and `specs/_template/spec.it.md` and fill them: Status `Draft`, author `<git user.name> (drafted by Claude)`, today's date.
- Fill every section. When the input does not say something, do **not** invent it: turn it into an Open question.
- Open questions: each names who decides, and quotes the transcript verbatim when a person's words are the evidence.
- Platform check: answer honestly, against the principles in `docs/VISION.md`. If the feature only makes sense for one domain, say that it belongs in a package or in the person's data, not in the engine, and flag it as an open question.
- Acceptance criteria in given/when/then, each checkable by a test or an observable result.
- `spec.it.md` is the faithful Italian version of `spec.md`: same sections, same criteria, same open questions.
- Add the spec to the index in `specs/README.md`.

Do not write `plan.md` or `tasks.md`, and do not write code.

## 3. Verify and report

- Re-read each spec against the input: every requirement from the input is either in the spec or explicitly a non-goal.
- Check that `spec.md` and `spec.it.md` match section by section.
- This repository is public: no personal data (people become roles, real data becomes synthetic examples), no secrets, no private references (host names, internal repositories, deploy details).
- Report: the specs created or updated (paths), and the open questions the humans must answer before `/plan`. Do not commit: the humans review first.
