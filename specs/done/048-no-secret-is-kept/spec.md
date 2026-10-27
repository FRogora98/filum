# No secret is kept, and nothing passing

> Status: Implemented (2026-10-19). Reviewed (the owner, 2026-10-19, Track A, a secret filter
> on capture and a durability gate on what is saved)
> Area: engine (events, files, facts, instructions), filum-mcp (instructions)
> Author/date: federico rogora (drafted by Claude), 2026-10-19

## Problem

People paste keys, tokens and passwords into their agents. A memory that keeps every message would keep them forever,
in plain files, in their history. A small personal-agent memory examined while designing this was found to hold a
token and a password in plain text. And a model that saves everything fills the memory with what will not matter in
the next session.

## Goal

A secret never reaches the memory, whatever path it takes. The agent saves what will still matter later.

## Desired behavior

1. **Every event and every file is kept with any secret replaced** by `[secret removed by Filum]`. This covers:
   - what was said, including the `log` hook;
   - imports;
   - every file written through the engine, which includes the facts table.
2. **What counts as a secret:**
   - the providers' own key formats (`sk-…`, `ghp_…`, `github_pat_…`, `AKIA…`, `xox…-`, `AIza…`);
   - JSON web tokens;
   - private key blocks;
   - a labelled value (`password:`, `api_key=`, `client secret =`, …), where the label stays and only the value goes.

   Text that only talks about passwords or secrets is left alone.
3. **A hand edit of a file is the person's own,** and is not changed.
4. **Spec 049, the durability gate.** The instructions and consolidation save only what will still matter in a later
   session: not small talk, not the passing details of the task at hand.

## Acceptance criteria

1. Each key format, a JSON web token, a private key block and a labelled password are removed. Sentences about
   passwords, secrets and tokens are unchanged (unit tests).
2. A key said in a message, written in a file and recorded as a fact is in none of them (a contract test on every
   store).
3. The instructions of both filum-mcp profiles carry the durability rule and stay under 4,096 characters.
