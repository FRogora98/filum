# Filum

**A memory and skills engine for AI agents.** Give any agent a memory it can read, a history it can audit, and
procedures it learns from you. Bring your own model.

> Status: pre-release. The engine is being extracted from a private codebase and published here step by step.
> Nothing to install yet. Watch the repo or come back in a few weeks.

## What it does

- **Memory as files with revisions.** Every fact lives in a plain file you can open. Every change is an append-only
  revision, so you can always see what the agent knew, when, and why it changed.
- **Skills: procedures, not just facts.** When you repeat a task, the agent proposes to save it as a skill. Next time,
  one call runs it on fresh data.
- **Typed collections.** Lists with a schema (people, decisions, anything), kept next to the free-form memory.
- **Works with small models.** Reliability is measured with evals against cheap models and the results are published.
- **Domain-free.** The engine knows nothing about your domain. Verticals are *packs*: a prompt, preinstalled skills,
  collection schemas and domain tools, loaded by configuration.

## Two ways in, one set of tools

1. **MCP server (stdio).** Run `filum-mcp` inside Claude Code, Claude Desktop, Codex or any MCP host. No API keys,
   no service: the host's model does the thinking, Filum keeps the memory in a local directory.
2. **Self-hosted service.** The same engine with its own agent loop, models and multi-user accounts, reachable via
   API and remote MCP.

Same tool names, same parameters, same semantics in both.

## Roadmap

Engine libraries, local directory memory, `filum-mcp`, then remote MCP, packs and hosting APIs. The detailed
specs will be published alongside the code.

## License

MIT. Contributions require signing the [CLA](CLA.md).
