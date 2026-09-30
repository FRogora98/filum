# Feature specs · Spec di feature

**EN** — Working documents for non-trivial changes to Filum. Every change to observable behavior, to a contract (tool, file format, configuration) or to the SDLC starts here, and is driven by the Claude Code commands in `.claude/commands/`.

**IT** — Documenti di lavoro per le modifiche non banali a Filum. Ogni modifica a un comportamento osservabile, a un contratto (strumento, formato dei file, configurazione) o all'SDLC parte da qui, guidata dai comandi di Claude Code in `.claude/commands/`.

## Flow · Flusso

```
/spec           →  NNN-name/spec.md + spec.it.md   (Draft → Reviewed: human review)
/plan NNN       →  NNN-name/plan.md + tasks.md     (contracts first, then small verifiable tasks)
/implement      →  code + tests, one task at a time (Implemented)
                →  done/NNN-name/                  (frozen history)

/fix-bug        →  bug: failing test → fix → verify → self-judge
/review-changes →  review of the uncommitted changes or of recent commits
```

## Index · Indice

| # | Spec | Status | Area |
|---|---|---|---|
| 013 | [filum-mcp: the engine as a local MCP server](013-filum-mcp/spec.md) · [filum-mcp: il motore come server MCP locale](013-filum-mcp/spec.it.md) | Reviewed | MCP |

## Rules · Regole

- Numbers are sequential with three digits; a number is never reused. · Numeri progressivi a tre cifre, mai riutilizzati.
- `spec.md` (EN) and `spec.it.md` (IT) always change together; `plan.md` and `tasks.md` are English only. · `spec.md` e `spec.it.md` cambiano sempre insieme; `plan.md` e `tasks.md` solo in inglese.
- This repository is public: no secrets, no personal data, no private references, no hard-wired domain; examples are synthetic. · Questo repository è pubblico: niente segreti, dati personali, riferimenti privati o domini cablati; esempi sintetici.
- Folders in `done/` are history and are never edited. · Le cartelle in `done/` sono storia e non si modificano.
