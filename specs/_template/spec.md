# <Feature name>

> Status: Draft | Reviewed | Implemented
> Area: <engine / local store / MCP / tools / SDLC>
> Author/date: <who>, <YYYY-MM-DD>

## Problem

<2–5 sentences: what does not work today, for whom, and what evidence exists (meeting notes, failing scenarios, logs, measurements). State the problem, not the solution.>

## Goal

<One sentence: the observable outcome once this is done.>

## Non-goals

<Bullet list of what is deliberately OUT of scope. This section prevents scope creep, so be explicit. E.g. "no new connector", "no change to the mobile app".>

## Current behavior

<How the system behaves today in the affected area. Point to the code (`file:line`) instead of re-describing it; only spell out what the code does not make obvious.>

## Desired behavior

<The new behavior as observable statements: what a user, a client or a downstream component sees, not how the code achieves it. Cover failure paths too: what happens when a service is down, when input is malformed, when a model returns garbage.>

## Platform check

<Answer both, in one line each:
1. Is this a generic building block, valid for any person and any domain? If it only makes sense for one domain, it belongs in a package or in the person's data, not in the engine.
2. Does it respect sensitivity levels on every tool it touches, keep the person's files readable and theirs, and add no secret, personal data or private reference to this public repository?>

## Acceptance criteria

<Numbered, each independently checkable, ideally each mapped to a test. Use the given/when/then shape. A criterion nobody can verify is a wish, not a criterion.>

1. Given <precondition>, when <action>, then <observable result>.
2. Given <failure precondition>, when <action>, then <observable failure handling>.
3. ...

## Open questions

<Decisions a human must take before plan.md can be written. Empty this section (moving the answers into the sections above) before starting the plan.>

- [ ] <question> → <answer once decided>
