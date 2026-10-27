# <Feature name> — plan

> Prerequisite: spec.md and spec.it.md are Reviewed (acceptance criteria agreed, open questions emptied).

## Approach

<The chosen design in a few paragraphs. If alternatives were considered, one line each on why they lost, enough for a future reader not to re-open the decision.>

## Constraints honored

<Which project rules shape this design and how the design satisfies each. Typical ones:
- generic in code, personal in data: no domain hard-wired in code, prompts, tool descriptions or tests;
- the person's files stay theirs: readable plain files, an append-only history, nothing hidden or sent off the machine;
- one person per memory, sensitivity levels enforced by every tool;
- deterministic where possible: arithmetic, dates and permissions in code, not in the model.>

## Contract changes

<THE key section, written before any code: the new or changed contracts, as they will look once built.
Tools (name, parameters, description, behavior), file formats on disk (folders, files, fields, invariants),
command line and configuration (name, default, effect).
Write "none" for the kinds this change does not touch.>

## Files touched

| File | Change |
|---|---|
| `src/...` | <what> |

## Risks and mitigations

<What could go wrong for users (data loss, privacy leak, wrong numbers, cost) and how the design limits the blast radius. Include the rollback story: how is this turned off or reverted?>

## Test strategy

<Which acceptance criteria map to which kind of test (unit, integration with Testcontainers, growth-test scenario, manual check), and what genuinely cannot be tested offline.>
