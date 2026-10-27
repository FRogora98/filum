# Contributing to Filum

Thank you for helping. Filum is a memory engine for one person's agent, so a change is judged first by one question:
**would it work for any person, in any domain, without new code?** Domain content (finance, health, work, sport...)
belongs in a package, not in the engine.

## Sign your commits (DCO)

Contributions are accepted under the [Developer Certificate of Origin](DCO) 1.1. You keep your copyright. By signing
off, you certify that you wrote the change, or have the right to submit it, under the project's MIT license.

Add a `Signed-off-by` line to every commit, with your real name and an email you can be reached at:

```sh
git commit -s -m "What the change does"
# Signed-off-by: Your Name <you@example.com>
```

A pull request whose commits are not all signed off fails its DCO check. To sign off the commits you already made,
run `git rebase --signoff main` and push again.

## Before you start

- **Small fixes** (a typo, a clear bug with a test): open a pull request directly.
- **Anything a user or a host can notice** (a tool, a parameter, the memory folder's format, the public API): open an
  issue first, then write a spec. The `specs/` folder has a template (`specs/_template/`): a spec says the problem,
  the desired behavior and testable acceptance criteria, and is reviewed before code is written.
- Read [`docs/VISION.md`](docs/VISION.md) (what Filum is for) and [`ARCHITECTURE.md`](ARCHITECTURE.md) (how it is
  built).

## Build, test, check

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), and nothing else:

```sh
dotnet build Filum.slnx
dotnet test Filum.slnx
bash scripts/check-secrets.sh
```

- **Warnings are errors**, and the analyzers and the architecture tests run in every build. The rules are in
  [`docs/CODING-STANDARD.md`](docs/CODING-STANDARD.md). An approved deviation is written in
  [`docs/EXCEPTIONS.md`](docs/EXCEPTIONS.md).
- **The public API** of each library is tracked in its `PublicAPI.Unshipped.txt`. A new public member is added there.
  A breaking change raises the major version.
- **The tool surface** is checked against a snapshot. After a deliberate change, run
  `FILUM_UPDATE_SNAPSHOT=1 dotnet test Filum.slnx` and review the diff.
- **A bug fix comes with a test** that fails before the fix and passes after it.
- **A refactoring changes no behavior.** A visible behavior change goes in a commit of its own, with its test, and in
  [`CHANGELOG.md`](CHANGELOG.md).
- **The benchmark harness** (`evals/bench/`) has its own tests:
  `python -m unittest discover -s evals/bench/tests`.

CI is defined to run the build, the tests and the secret scan on Linux, Windows and macOS for every pull request. While
it is paused (since 2026-10-27), the three commands above and the harness's tests are the gate: all of them pass locally
before every push.

## What never goes in

- **Secrets:** keys, tokens, connection strings, `.env` files.
- **Personal data:** tests and examples are synthetic ("a person", `example.invalid`). Never use a real name, email,
  address or memory.
- **Domain content** in code, prompts, tool descriptions or tests.

## Conduct and security

Everyone taking part follows the [Code of Conduct](CODE_OF_CONDUCT.md). Report a vulnerability privately, as
[SECURITY.md](SECURITY.md) says, never in a public issue.
