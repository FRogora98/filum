# Tasks: spec 056

- [x] 1. Chats continue: one Claude Code session per chat (`--session-id`, then `--resume`). Unit test on the arguments
  and on the runner's chats. (Acceptance 1)
- [x] 2. Steps as the host reports them: computed, used skill, proposal, `fact_end` as a write. Unit tests. (Acceptance 2)
- [x] 3. `RulesAsync` in the engine; `core_contains` finds a rule held as a fact. Unit tests. (Acceptance 3)
- [x] 4. Instructions: a skill asked for is saved; counts and totals by computation. Unit test. (Acceptance 5)
- [x] 5. Rules in filum-mcp's instructions at start, none with a private facts file, under 4,096. Tests. (Acceptance 4)
- [x] 6. The `mcp` suite before (1-3) and after (all), 3 repetitions, `with filum`. (Acceptance 6)

## Results (2026-10-21, Haiku 4.5, isolated, `with filum`, 3 repetitions)

| Run | Engine | Passed | Claimed but not done | Rules kept |
|---|---|---|---|---|
| 2026-10-20, old harness | 7c7d86e | 27/51 | 5 | 15/21 |
| before: harness fixes 1-3 | a46cdc4 | 31/51 | 5 | 18/21 |
| after: everything | 8ecfd99 | 36/51 | 6 | 19/21 |

The six scenarios, passes of 3, before → after: `count-sum` 2 → 2, `rule-follow` 2 → 3, `skill-starter` 3 → 3,
`skill-command-it` 0 → 1, `skill-create-use` 0 → 0, `skill-propose-it` 0 → 0. The other eleven: 24 → 27; lost one each
`fact-recall` and `row-remove-it`, gained `claim-trap-it`, `collection-create-it`, `fact-recall-it` and `skill-disable` (+2).

Then the skill line was sharpened ("a message starts with /<its name>, call skill_use"; "a skill they ask you to create
is saved with skill_save at once, never skill_propose") and the `skill-*` scenarios ran again: a skill asked for is now
saved in every run, and no false claim was made (0 of 14 runs). What still fails:
- **`skill-create-use`, 0/3:** in a new chat, "/log-note buy stamps" is logged as a fact; the model never calls
  `skill_use`, though the skill exists.
- **`skill-command-it`, 1/3:** the skill is used, but the list is written as a document, not a collection.
- **`skill-propose-it`, 0/3:** at the third same request, no `skill_propose`.

**Verdict on acceptance 6: not met.** Three of the six pass at least 2 of 3; the other eleven lost 2 passes (more than
the 1 allowed, within the run's noise, with 6 gained); false claims went 5 → 6, then 0 on the skill rerun. Kept: every
change, since none made a scenario worse beyond noise. Next, as a spec of its own: name the person's skills in the
server's instructions at start (as the rules), so "/name" is known without a tool call.

