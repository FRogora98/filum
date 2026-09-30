# Filum

**A memory and skills engine for AI agents**, open source (MIT). One person runs Filum on their own machine as an MCP server (`filum-mcp`) inside their agent (Claude Code, Claude Desktop, Codex, Cursor…): the host's model does the thinking, Filum keeps the memory in a local folder of plain files with an append-only history, and learns procedures (skills) from the person. No API keys, no service, no account.

The engine is also used, as a library, by a hosted product that lives elsewhere; nothing of that product belongs here.

**Pre-release.** The engine is being extracted from a private codebase and published here step by step, with a clean history. Read `docs/VISION.md` before designing anything.

## Layout

| Folder | Content |
|---|---|
| `docs/` | **what Filum is for** (functional, not implementation): `VISION.md` (the problem, the thesis, the principles, what Filum is not) |
| `src/` | the code (to come): `Filum.Engine` (memory, collections, skills, the tool catalog; no web, no database, no model), the local store, `Filum.Mcp` (the `filum-mcp` server) |
| `tests/` | the tests (to come): the engine's contract runs on every store, with no network and no Docker |
| `specs/` | feature specs (spec-driven development) |

## Verification commands

To be written with the first code (spec 014 of the private roadmap brings `src/` here). Until then there is nothing to build.

A change is done only when the commands for the part it touched pass. Git is trunk-based: work on `main`, small commits, no other branches.

## This repository is public

- **No secrets:** no keys, no tokens, no connection strings, no `.env`.
- **No personal data:** examples and tests are synthetic ("a person", `example.invalid`), never a real name, email, address or memory.
- **No private references:** no host names, IPs, tailnet names, internal repositories, deploy details, or the hosted product's internals. When unsure, leave it out and ask.
- **No domain content:** nothing about finance, health, work, sport… in code, prompts, tool descriptions or tests. Verticals are packages that live with the products that own them.
- Contributions require the [CLA](CLA.md).

## Development cycle

The AI-native cycle runs in local Claude Code through the commands in `.claude/commands/`: `/spec` (idea or notes → spec draft), `/plan` (reviewed spec → plan and tasks), `/implement` (tasks → code and tests), `/fix-bug` (bug → failing test → fix), `/review-changes` (review of changes). Humans review the spec before `/plan` and decide what gets pushed: pushing here publishes.

## The product rule

**Generic in code, personal in data.** Filum is an engine from which a personal memory *emerges* for each person; it is never tailored to one person or one domain. Every capability is a generic building block (files with history, collections with schemas, procedures as data). Before adding anything, ask: *would this work for any person, in any domain, without new code?*

## Spec-driven development

Non-trivial changes (observable behavior, a contract, a tool) start in `specs/`: copy `specs/_template/`, write `spec.md` + `spec.it.md`, get human review on the document, then `plan.md` and `tasks.md`.

## Work in phases: understand → change → verify

Finish one phase before starting the next and say which one you are in. If a later phase contradicts an earlier conclusion, go back to that phase instead of patching forward.

1. **Understand** (read only): read the spec first; trace the real flow end to end; never hide a symptom behind a guard. Before changing shared code, find every caller.
2. **Change** (smallest thing that works): do only what was asked; reuse what exists; no abstraction nobody asked for.
3. **Verify** (before claiming anything): run the tests and read the output; a bug fix gets a test that fails before and passes after. Keep verified facts separate from what you could not check, and say plainly when something was not run.
