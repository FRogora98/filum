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

Numbers 017, 019, 024, 033 and 034 are not used here. · I numeri 017, 019, 024, 033 e 034 non sono usati qui.

| # | Spec | Status | Area |
|---|---|---|---|
| 055 | [Capture and adherence without the host's nudges](done/055-capture-and-adherence-without-nudges/spec.md) · [Memorizzare e rispettare le regole senza le spinte dell'host](done/055-capture-and-adherence-without-nudges/spec.it.md) | Implemented | engine, MCP |
| 056 | [What the mcp suite sees, skills on request and rules in a fresh chat](done/056-what-the-mcp-suite-sees/spec.md) · [Cosa vede la suite mcp, le skill su richiesta e le regole in una chat nuova](done/056-what-the-mcp-suite-sees/spec.it.md) | Implemented | engine, MCP, evals |
| 057 | [Counting across conversations](057-counting-across-conversations/spec.md) · [Contare attraverso le conversazioni](057-counting-across-conversations/spec.it.md) | Draft | engine, MCP |
| 050 | [It knows who you are: a measure](done/050-it-knows-who-you-are/spec.md) · [Sa chi sei: una misura](done/050-it-knows-who-you-are/spec.it.md) | Implemented | evaluation |
| 048 | [No secret is kept, and nothing passing](done/048-no-secret-is-kept/spec.md) · [Nessun segreto si tiene, e niente di passeggero](done/048-no-secret-is-kept/spec.it.md) | Implemented | engine, MCP |
| 046 | [Rules and open items, caught even when a turn misses them](done/046-rules-and-open-items/spec.md) · [Regole e cose aperte, prese anche quando un turno le perde](done/046-rules-and-open-items/spec.it.md) | Implemented | engine, MCP |
| 045 | [It knows you from the first message](done/045-it-knows-you-from-the-first-message/spec.md) · [Ti conosce dal primo messaggio](done/045-it-knows-you-from-the-first-message/spec.it.md) | Implemented | engine, MCP |
| 044 | [Ready for outsiders](done/044-ready-for-outsiders/spec.md) · [Pronto per chi viene da fuori](done/044-ready-for-outsiders/spec.it.md) | Implemented | documentation, CI |
| 043 | [A benchmark anyone can run](done/043-a-benchmark-anyone-can-run/spec.md) · [Un benchmark che chiunque può lanciare](done/043-a-benchmark-anyone-can-run/spec.it.md) | Implemented | evaluation |
| 042 | [Recall anchored in facts](done/042-recall-anchored-in-facts/spec.md) · [Richiamo ancorato ai fatti](done/042-recall-anchored-in-facts/spec.it.md) | Implemented | engine, embeddings, MCP |
| 041 | [A ledger of what holds](done/041-a-ledger-of-what-holds/spec.md) · [Un registro di ciò che vale](done/041-a-ledger-of-what-holds/spec.it.md) | Implemented | engine, tools |
| 040 | [A clean agent interface](done/040-a-clean-agent-interface/spec.md) · [Un'interfaccia pulita per l'agente](done/040-a-clean-agent-interface/spec.it.md) | Implemented | engine, MCP |
| 039 | [A history you can trust](done/039-history-you-can-trust/spec.md) · [Una storia di cui fidarsi](done/039-history-you-can-trust/spec.it.md) | Implemented | engine, storage |
| 038 | [Read a whole conversation](done/038-read-a-conversation/spec.md) · [Leggere una conversazione intera](done/038-read-a-conversation/spec.it.md) | Implemented | engine, tools |
| 037 | [Values that change: the old one stays in sight](done/037-values-that-change/spec.md) · [Valori che cambiano: il vecchio resta in vista](done/037-values-that-change/spec.it.md) | Implemented | engine, tools |
| 036 | [Every message kept, whatever the model](done/036-every-message-kept/spec.md) · [Ogni messaggio conservato, qualunque sia il modello](done/036-every-message-kept/spec.it.md) | Implemented | MCP, evals |
| 022 | [The fast gate: keep or discard](done/022-keep-gate/spec.md) · [Il filtro veloce: tenere o scartare](done/022-keep-gate/spec.it.md) | Implemented | engine, consolidation |
| 027 | [What Filum learns about the person](done/027-learned/spec.md) · [Cosa Filum impara della persona](done/027-learned/spec.it.md) | Implemented | engine, the turn, MCP |
| 026 | [Trends: forecasts and what stands out](done/026-trends/spec.md) · [Tendenze: previsioni e cose che spiccano](done/026-trends/spec.it.md) | Implemented | engine, tools |
| 023 | [Hybrid retrieval](done/023-hybrid-retrieval/spec.md) · [Ricerca ibrida](done/023-hybrid-retrieval/spec.it.md) | Implemented | engine, MCP, hosting |
| 021 | [Absorb: imports from files](done/021-imports/spec.md) · [Assorbire: gli import da file](done/021-imports/spec.it.md) | Implemented | engine, MCP, HTTP |
| 032 | [Compute: the agent's own code](done/032-compute/spec.md) · [Calcolo: il codice dell'agente](done/032-compute/spec.it.md) | Implemented | engine, tools |
| 031 | [Lean turns](done/031-lean-turns/spec.md) · [Turni leggeri](done/031-lean-turns/spec.it.md) | Implemented | tools, the turn |
| 030 | [The memory core](done/030-memory-core/spec.md) · [Il nucleo della memoria](done/030-memory-core/spec.it.md) | Implemented | engine, the turn |
| 029 | [Episodic memory](done/029-episodic-memory/spec.md) · [Memoria episodica](done/029-episodic-memory/spec.it.md) | Replaced by 030 | the turn, tools |
| 020 | [The benchmark: LongMemEval on Filum](done/020-benchmark/spec.md) · [Il benchmark: LongMemEval su Filum](done/020-benchmark/spec.it.md) | Implemented | evals |
| 018 | [Evals for any host](done/018-evals-for-any-host/spec.md) · [Eval per qualunque host](done/018-evals-for-any-host/spec.it.md) | Implemented | evals |
| 016 | [Packages: a vertical as data](done/016-packages/spec.md) · [Pacchetti: un verticale fatto di dati](done/016-packages/spec.it.md) | Implemented | engine, MCP |
| 013 | [filum-mcp: the engine as a local MCP server](done/013-filum-mcp/spec.md) · [filum-mcp: il motore come server MCP locale](done/013-filum-mcp/spec.it.md) | Implemented | MCP |

## Rules · Regole

- Numbers are sequential with three digits; a number is never reused. · Numeri progressivi a tre cifre, mai riutilizzati.
- `spec.md` (EN) and `spec.it.md` (IT) always change together; `plan.md` and `tasks.md` are English only. · `spec.md` e `spec.it.md` cambiano sempre insieme; `plan.md` e `tasks.md` solo in inglese.
- This repository is public: no secrets, no personal data, no private references, no hard-wired domain; examples are synthetic. · Questo repository è pubblico: niente segreti, dati personali, riferimenti privati o domini cablati; esempi sintetici.
- Folders in `done/` are history and are never edited. · Le cartelle in `done/` sono storia e non si modificano.
