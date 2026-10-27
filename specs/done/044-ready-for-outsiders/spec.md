# Ready for outsiders

> Status: Implemented (2026-10-18). Reviewed (the owner, 2026-10-18, step C6 of the plan; the DCO instead of the CLA)
> Area: documentation, CI, contribution terms
> Author/date: federico rogora (drafted by Claude), 2026-10-18

## Problem

The repository is written as a public one, but a newcomer is missing several things:
- **Results.** The README says what Filum does, but not how it measured against others, with what limits, or how it
  compares with Mem0, Zep and Letta.
- **Contribution terms.** The terms are a CLA, and the files that tell a newcomer how to contribute, report a
  vulnerability or behave are missing.
- **The coding standard** every change must follow is in Italian.
- **CI** runs on Linux only, while `filum-mcp` ships for Windows, macOS and Linux.

## Goal

An outsider who opens the repository finds:
- what Filum is, how to start, how it is built, and how it measured, honestly;
- how to contribute under the DCO, how to report a vulnerability, and the code of conduct;
- the coding standard in English;
- CI that proves every change on the three systems Filum ships for.

## Non-goals

- No code change in the engine or `filum-mcp`.
- No release and no publication of the repository: that is the owner's.

## Desired behavior

1. **README:**
   - "Results, with their limits": the pre-registered rounds as they came out, and their limits stated plainly;
   - "How it compares": Mem0, Zep/Graphiti, Letta, only on what their own sources state;
   - "How it is built": a short summary, pointing to `ARCHITECTURE.md`;
   - one command for the public benchmark (spec 043).
2. **Contribution terms:**
   - `CONTRIBUTING.md`: the DCO sign-off, the spec-driven flow, the commands, and what never goes in;
   - `DCO` holds the Developer Certificate of Origin 1.1, verbatim;
   - `CLA.md` is removed.
3. **`SECURITY.md`:**
   - private reporting through GitHub's security advisories;
   - what Filum does and does not reach (files, network, sandbox);
   - what is in scope.
4. **`CODE_OF_CONDUCT.md`:** the Contributor Covenant 2.1, by reference, with how to report.
5. **The coding standard:**
   - `docs/CODING-STANDARD.md` is in English: every rule, ID, level and check kept;
   - the Italian original is its twin, `docs/CODING-STANDARD.it.md`.
6. **CI:**
   - build and tests on `ubuntu-latest`, `windows-latest` and `macos-latest`, on every push to `main` and every pull
     request;
   - the secret scan and the benchmark harness's tests on Linux;
   - a DCO check that fails a pull request with a commit not signed off.
7. **`CHANGELOG.md`** lists the change for contributors.

## Platform check

1. Generic: documentation and CI only.
2. No secrets, personal data or private references. The results cite public benchmarks, and the comparison cites
   public sources.

## Acceptance criteria

1. Given the README, then it has the four sections of item 1. Every number in "Results" is one of the pre-registered
   outcomes. Every limit the engine audit listed for the evaluation is stated.
2. `CLA.md` is gone; `DCO`, `CONTRIBUTING.md`, `SECURITY.md` and `CODE_OF_CONDUCT.md` exist, and the README's license
   line points to the DCO.
3. `docs/CODING-STANDARD.md` is in English with the same 96 rule IDs, in the same order, as `docs/CODING-STANDARD.it.md`.
4. The engine's tests pass on Linux, run in a Linux container before the CI change is pushed. Windows is this
   machine.
5. The CI workflow has the three-system matrix and the DCO job. Its first run on `main` is green, or its failures are
   fixed before the next step.
