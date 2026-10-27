# Security

## Reporting a vulnerability

Report it privately through GitHub: **Security → Report a vulnerability** on this repository. That opens a private
advisory that only the maintainers can see. Do not open a public issue.

Please include what you found, how to reproduce it, and what an attacker could do with it. You will get an answer
within 7 days. A confirmed vulnerability is fixed in a release, and the advisory is published with credit to you
unless you prefer otherwise.

## Supported versions

Fixes go into the latest 1.x release. Upgrade to it to receive them.

## What Filum is, for security

- **`filum-mcp` runs on your machine, for one person.**
  - It reads and writes only its memory folder (`FILUM_HOME`, or `~/.filum`).
  - It talks to the agent that started it over stdio. It opens no network port.
- **It has no model, key or account of its own.** The agent's host decides which model reads the memory.
- **It goes online only to download files**, and only these:
  - **The search model,** downloaded on first start. Turn it off with `FILUM_EMBEDDINGS=off`.
  - **The reranker,** only with `FILUM_RERANKER=on`.
  - Each download is pinned to a fixed revision and used only when its SHA-256 matches.
- **The memory is plain files.** Anyone who can read the folder can read the memory. Protect it as you protect your
  documents: disk encryption, backups, file permissions.
- **Code from the agent runs in a sandbox.** `compute_run` and skills run JavaScript with Jint, with no file system,
  network or host access, and with limits on time, memory and statements.

In scope:
- a way to read or write outside the memory folder;
- an escape from the JavaScript sandbox;
- a downloaded file used without a matching hash;
- a crash or corruption of the memory caused by input an agent can send.

Out of scope:
- what the agent's own model does with what it reads;
- a person who can already read the memory folder.
