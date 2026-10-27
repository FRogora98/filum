# Tasks (spec 055)

1. [x] **Today and planned.**
   - `CurrentAsync` (no `on`) means held today.
   - `PlannedAsync` gives values that start later.
   - The brief gets a "Planned" section, `facts_current` adds planned lines, and recall matches planned facts too.
   - Tests: unit tests for the brief, recall and the tool (acceptance 1).
2. [x] **`FILUM_TODAY`** in filum-mcp: a clock for the server, the hooks and the store. Unit test.
3. [x] **Rules before the answer.**
   - Recall ends with "Before you answer, follow: …".
   - `InstructionParts.RuleReach` goes into the instructions.
   - Tests (acceptance 2).
4. [x] **Abstention and capture without asking.**
   - `InstructionParts.Unknown` and `WithoutAsking` go into the instructions and the tidy rules.
   - Tests (acceptance 4).
5. [x] **Closures with their event.**
   - `fact_end` takes `sources`.
   - The tidy rules require the closing event.
   - The snapshot is regenerated and reviewed.
   - Tests (acceptance 3).
6. [x] **The first overview.**
   - Without hooks, the first `memory_overview` gives the full brief and the unsaved line, within the cap.
   - Tests.
7. [x] **Bench.**
   - `FILUM_TODAY` is set per session and probe.
   - False "I do not know" answers on said facts are counted in the report.
   - First-answer added time is reported.
8. [x] **Person 4,** frozen before any run, with the content acceptance 5 names. Evidence words.
9. [x] **Runs on the isolated harness, one at a time:**
   - person 4 and people 1-3, with Haiku hooks and no hooks, and gpt-4o-mini hooks;
   - the `mcp` scenario suite before and after.

## Results (2026-10-20)

- **Acceptance 5 met on person 4** (Haiku 4.5): with hooks 22/23, without 20/23 (18/23 after the `memory_log` change);
  rules 3/3 and both abstentions with hooks. People 1-3 with hooks: 22, 22, 20.
- **Acceptance 6 met** on the `mcp` scenario suite at 3 repetitions: with filum 18/51 before, 27/51 after, claimed but
  not done 11 → 5 (paired 11 to 2, p = 0.023). It took a second change: `memory_log` answering that nothing is saved
  yet, and "save without being asked" as a line of its own.

## Backlog for the next spec

Scenarios that fail in every run, before and after this spec: `count-sum`, `rule-follow`, `skill-command-it`,
`skill-propose-it`, `skill-starter`, and `skill-create-use` (no longer a false claim, still not done). Not worked on
here.
