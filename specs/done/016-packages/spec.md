# Packages: a vertical as data

> Status: Implemented
> Area: engine / MCP
> Author/date: federico rogora (drafted by Claude), 2026-09-30
> Roadmap: 016, synchronized with the first product that hosts the engine (its package is the first real one)

## Problem

The engine is generic on purpose: it knows no domain. A product that wants a vertical agent (a coach for one subject, an assistant for one kind of work) needs to start every new memory with its own guidance, the procedures it knows from day one, and the kinds of lists it keeps.

Today the only way is code: the starter skills are a constant in `Skills.Starters`, the core template is `PlatformInstructions.CoreTemplate`, and there is no place at all for a product's own rules. The product rule forbids putting a domain in code, so the vertical must be data, loaded by configuration, with the engine unchanged.

## Goal

A package is a folder of plain files. A host (`filum-mcp`, or a product hosting the engine) points the engine at it, and from then on every new memory starts with the package's files and skills, and the agent follows the package's rules. Without a package, the engine is the generic Filum, exactly as today.

## Non-goals

- **No domain tools:** a package is data only. Tools of a domain are the host's code (spec 017) or, inside an agent, other MCP servers the host mounts.
- **No updates to existing memories:** a package is applied when a memory is created. What happens to memories created with an older version of it is a later spec (see the open question).
- **No package registry, download or signing:** a package is a folder the host already has.
- **No more than one package at a time.**
- **No real vertical package in this repository:** only a minimal, domain-free example. Real packages live with the products that own them.

## Current behavior

- `MemoryService.EnsureCoreAsync` (`src/Filum.Engine/MemoryService.cs`) creates, the first time, the core `/filum.md` from `PlatformInstructions.CoreTemplate` and saves the `Skills.Starters` as `/skills/<name>.md` files.
- Skill files have a fixed format, parsed and checked by `Skills` (`src/Filum.Engine/Skills.cs`).
- A collection is a `.csv` file whose header line is its schema.
- The instructions of a turn are the platform layer (`PlatformInstructions.Text`, or `McpInstructions.Text` in `filum-mcp`), then the person's core, the index and the skills. The core "adds to these instructions and never overrides them".

## Desired behavior

**The format.** A package is a folder:

```
pack.json        required: {"format": 1, "name": "journal", "version": "1.0.0", "description": "One line."}
prompt.md        optional: the package's rules and guidance for the agent, plain text
skills/*.md      optional: starter skills, in the skill file format
memory/**        optional: the files a new memory starts with, at the same paths
                 (documents .md, collections .csv with their header, /filum.md to replace the core template)
```

- `format` is the version of the package format: this spec defines 1. A package with a format the engine does not know is refused with a clear message, never half read. `version` is the package's own version, for its author.
- `pack.json` may list `"private": ["/path", …]` and `"sensitive": [...]`: files of `memory/` created with that sensitivity.
- Everything is checked with the engine's own rules, before any memory is touched:
  - skill files parse, and their names are valid;
  - paths are valid memory paths;
  - collections have a header;
  - every file is within the size limits;
  - the files, the engine's starter skills and the package's skills together are within the file count.
- A package with any problem is refused as a whole, with every problem listed. Nothing of it is used.

**Loading.**
- `filum-mcp` reads `FILUM_PACK` (a folder path).
- A host passes the folder to the engine by its own configuration, for example `Memory:PackPath`.
- At start the host logs the name and version of the package it loaded (from `pack.json`), so a running instance shows which package it uses.
- A package that is missing or refused stops the host at start, with the list of problems on stderr or in the log. Filum never runs with half a package, and never silently without one.

**A new memory with a package.** When a person's memory is created:
- the core comes from the package's `memory/filum.md` if there is one, otherwise from the template;
- the package's `memory/` files are created with the author "platform", with the listed sensitivity;
- the engine's starter skills are saved, then the package's skills; a package skill with the same name as a starter replaces it;
- the revision history shows all of it as the platform's, so it can be undone like anything else.

**The package's prompt.** It is placed right after the platform layer and before the person's core, with the same standing: the person's core adds to it and never overrides it. A product's non-negotiable rules therefore hold whatever the person writes in their core.
- The prompt is the first line of a product's guardrails, not the only one: what must never happen is enforced by the host's code (spec 017), for example tools left out with `ExcludedTools`, or proposals the person confirms instead of writes.
- In the hosted loop, it is part of every turn's instructions.
- In `filum-mcp`, it is added to the server's instructions, and `memory_overview` returns it at the top.

**Nothing else changes.** The tools, their names and their descriptions are the same, and the catalog snapshot does not move.
- A memory created before the package was set keeps its files and skills.
- The prompt is read from the package, never copied into a memory, so it applies to every memory from the next turn.

**The example package.** `packs/example/` in this repository is small and domain-free:
- a prompt of a few lines;
- one starter skill;
- one collection with a header;
- one document.

Tests and the README use it.

## Platform check

1. **Generic:** yes. The format and the loader know no domain; the domain is only in the data a product supplies. The example package names none.
2. **Sensitivity and privacy:**
   - package files get the sensitivity the package declares, and the engine enforces it as for any file;
   - a package holds no personal data: it is the same for every person, and it is applied to a memory only when that memory is created;
   - nothing private enters this repository: the example is synthetic.

## Acceptance criteria

1. Given the example package, when a new memory is created with it, then it has the package's core or the template, its files at their paths with the declared sensitivity, the engine's starter skills and the package's skills, all with revisions by the platform.
2. Given no package, when a new memory is created, then it is exactly as today: the template core and the starter skills.
3. Given a package with a broken skill file, an invalid path and a collection without a header, when it is loaded, then it is refused, the three problems are listed, and no memory is touched.
4. Given a package with a prompt, when a turn's instructions are composed (hosted loop) or `filum-mcp` starts, then the prompt comes after the platform layer and before the person's core. `memory_overview` starts with it.
5. Given a person's core with a rule that contradicts the package's prompt, when the instructions are composed, then the platform text still says the core never overrides what comes before it.
6. Given a memory created before a package was set, when the package is set, then that memory's files and skills are unchanged, and the package's prompt applies to its next turns (the prompt is read from the package, never copied into a memory).
7. Given `FILUM_PACK` pointing to a missing or refused package, when `filum-mcp` starts, then it exits with a non-zero code and the problems on stderr.
8. Given the catalog snapshot, when the package feature is added, then the snapshot is unchanged.

## Open questions

None. Answered by the owner on 2026-09-30:

- **Memories that already exist when a package changes version:** postponed ("ci penseremo più avanti"). Until then:
  - the prompt always applies, because it is read at each turn, not copied;
  - files and skills are applied only when a memory is created.

  A later spec may add "give existing people what they do not have yet, never overwrite what they changed". The owner leans toward an **agentic porting**: the agent itself brings a person's memory up to a new version of the package, with the person's changes kept ("io penserei ad un sistema di porting agentico").
