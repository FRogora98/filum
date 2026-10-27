# Filum

**A memory and skills engine for AI agents** (MIT, public). The host's model does the thinking; Filum keeps the memory
in a local folder of plain files with an append-only history. What it does: `README.md`. What it is for:
`docs/VISION.md` (read it before designing anything). How it is built: `ARCHITECTURE.md`. How to contribute (build,
test, the public API, the snapshot, DCO, what never goes in): `CONTRIBUTING.md`.

## Invariants

- **No model of its own:** the engine thinks with its host's model (`filum-mcp`) or with the one a standalone use plugs
  in (`samples/Filum.Standalone`). Nothing that needs a model, a key, a database or a web server goes in.
- **Verification:** `dotnet build Filum.slnx`, `dotnet test Filum.slnx`, `bash scripts/check-secrets.sh` and
  `python -m unittest discover -s evals/bench/tests`, all green before every push; CI runs them on Linux, Windows and
  macOS. A change is done only when they pass.
- **Never regenerate** the v0.7.1 folder fixture (`tests/Filum.Engine.Storage.LocalFolder.IntegrationTests/Fixtures/folder-v0.7`):
  it must always be read and continued.
- **Versions:** semantic versioning, one tag per release (`vX.Y.Z`, which runs `.github/workflows/release.yml`). Tag
  only when the owner asks; the CHANGELOG says what each version changes.
- **Public repository:** no secrets, no personal data (examples are synthetic), no private references (hosts,
  internal repositories, deploys, a hosted product's internals), no domain content. When unsure, leave it out and ask.
  Every commit is signed off (`git commit -s`).
- **Generic in code, personal in data:** every capability is a generic building block; before adding anything ask:
  *would this work for any person, in any domain, without new code?*
- **Spec-driven:** non-trivial changes start in `specs/` (see `specs/README.md`); a human reviews the spec before
  `/plan`. Git is trunk-based: `main`, small commits.

## Work in phases: understand → change → verify

Finish one phase before starting the next and say which one you are in. If a later phase contradicts an earlier
conclusion, go back to that phase instead of patching forward.

1. **Understand** (read only): read the spec first; trace the real flow end to end; never hide a symptom behind a guard.
   Before changing shared code, find every caller.
2. **Change** (smallest thing that works): do only what was asked; reuse what exists; no abstraction nobody asked for.
3. **Verify** (before claiming anything): run the tests and read the output; a bug fix gets a test that fails before and
   passes after. Keep verified facts separate from what you could not check, and say plainly when something was not run.
   Behavior stays unchanged in a refactoring; a visible behavior change goes in a commit of its own, with its test.
