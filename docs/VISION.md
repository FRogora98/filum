# Vision

**A memory your agent can read, a history you can audit, and procedures it learns from you.**

## The problem

Every AI agent forgets. Each session starts from zero; what the person explained yesterday, how they want things done, the lists they keep: all of it has to be said again, or it lives in a chat history nobody can read. The memory features that exist either extract "facts" into a vector store the person never sees, or are locked inside one assistant.

Whoever builds this alone, with a capable agent and a folder of files, discovers that **it works surprisingly well**: an agent that can read and write its own notes, rules and procedures quickly becomes indispensable. What is missing is the discipline: a history of every change, typed lists, procedures that run, privacy levels, and tools designed so that even a small model uses them well.

## The thesis

1. **The memory belongs to the person, as files.** Plain files they can open, edit, move or put under version control; every change an append-only revision, so they can always see what the agent knew, when, and why it changed.
2. **Procedures, not just facts.** When the person repeats a task, the agent proposes to save it as a skill; next time one call runs it on fresh data.
3. **The model is the host's.** Filum does not need a model of its own: the agent the person already uses does the reasoning, guided by the tools' descriptions.
4. **Reliable with small models, measured.** The tools are shaped so that cheap models use them correctly, and the evals that prove it are published.

## Principles

1. **Generic in code, personal in data.** No domain is hard-wired: what a memory holds is entirely the person's.
2. **Deterministic where possible, model where needed.** Paths, checks, totals over a collection, history and undo are tested code; the model chooses what to do.
3. **Never invent.** A fact without a source is not a fact; the tools report what they did, never what the model says it did.
4. **Inspectable before powerful.** Every change can be seen, understood and undone.
5. **Privacy is a property of the system.** Sensitivity levels hold on every tool; private content never surfaces unprompted; nothing leaves the person's machine.
6. **One engine.** The same engine powers every product built on it; a fix is made once.

## What Filum is not

- Not a chatbot, not an app: it is the memory beneath the agent the person already uses.
- Not a vector store of extracted facts: memory is readable files with a history.
- Not a service: it runs on the person's machine, for one person, with no account and no key.
- Not tied to a domain: verticals are packages, and they live with the products that own them.
