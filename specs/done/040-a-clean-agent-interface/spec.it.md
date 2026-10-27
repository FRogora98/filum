# Un'interfaccia pulita per l'agente

> Stato: Implementata (2026-10-18). Rivista (il titolare, 2026-10-18, gap 5 dell'audit del motore; la politica sui fatti e i profili di default decisi lo stesso giorno)
> Area: motore (strumenti, istruzioni), filum-mcp
> Autore/data: federico rogora (scritta da Claude), 2026-10-18

## Problema

L'audit indipendente ha trovato l'interfaccia dell'agente indietro rispetto alla pratica attuale:
- **Gli errori sono un prefisso di testo.** Una chiamata rifiutata restituisce `"Refused: …"` (`MemoryToolGroup.Run`), e
  filum-mcp riconosce il prefisso e lancia un'eccezione (`PerCallFunction`): un host la vede come errore di protocollo,
  non come risultato che il modello può leggere.
- **Nessuna annotazione.** Un host non distingue `memory_delete` da `facts_current`: niente `readOnlyHint`,
  `destructiveHint` o `idempotentHint`.
- **Nessun output strutturato.** Fatti e risultati di ricerca tornano come prosa; un host o una valutazione non li può
  leggere in modo affidabile.
- **Due politiche sui fatti.** Le istruzioni di filum-mcp dicono di registrare ogni fatto della persona con
  `fact_record`; quelle della piattaforma dicono che i fatti sulla persona vanno nel core o nei documenti, `fact_record`
  solo per valori che cambiano. Le istruzioni stanno in testi separati che si sono allontanati.
- **32 strumenti insieme.** Molti più di quanto si consiglia oggi; non c'è un profilo ridotto.

## Obiettivo

Un host riceve risultati tipizzati, annotazioni e dati strutturati; una sola politica sui fatti arriva a ogni modello da
una sola fonte; chi inizia con filum-mcp parte da pochi strumenti.

## Fuori scopo

- Nessuno strumento nuovo e nessun cambiamento a ciò che uno strumento fa alla memoria.
- Nessun cambiamento al testo che un modello legge per una chiamata riuscita.

## Comportamento desiderato

1. **Rifiuti tipizzati.** La sessione degli strumenti registra se l'ultima chiamata è stata rifiutata. filum-mcp
   restituisce una chiamata rifiutata come risultato con `isError: true` e il motivo come testo, non come errore di
   protocollo. Gli host in-process vedono lo stesso testo di prima.
2. **Annotazioni.** Ogni strumento del motore porta `readOnlyHint`, `destructiveHint` e `idempotentHint` (e
   `openWorldHint: false`), da una sola tabella nel motore (`ToolTraits`). Gli strumenti di lettura sono in sola
   lettura; cancellazioni e undo sono distruttivi; gli altri scrivono senza distruggere.
3. **Output strutturato** per `facts_current`, `facts_history` ed `events_search`: oltre al testo, filum-mcp restituisce
   `structuredContent` (i fatti con soggetto, attributo, valore, da, a; le corrispondenze con id, data, chi e testo) e
   dichiara il suo `outputSchema`.
4. **Una politica sui fatti, una fonte.** Le parti comuni delle istruzioni (come tenere ciò che la persona dice, la
   politica sui fatti, come cercare ciò che è stato detto) sono scritte una volta nel motore (`InstructionParts`) e
   composte sia in `PlatformInstructions.Text` sia nelle istruzioni di filum-mcp. La politica: `fact_record` per ogni
   fatto puntuale che può cambiare, su una persona, un luogo o una cosa (dove vive qualcuno, un lavoro, quanti, uno
   stato, una preferenza), e il core e i documenti per chi è la persona e per i testi lunghi (note, piani). Un test
   controlla che le due superfici abbiano lo stesso testo della politica.
5. **Profili.** `core` (13 strumenti: `memory_overview`, `memory_read`, `memory_search`, `events_search`, `events_read`,
   `memory_write`, `memory_edit`, `memory_append`, `memory_delete`, `fact_record`, `facts_current`, `facts_history`, e
   `memory_log` di filum-mcp) e `full` (tutti). filum-mcp parte da `core`, si cambia con `FILUM_TOOLS=full` o con la sua
   configurazione; il default della libreria resta `full`. Le istruzioni nominano solo strumenti del profilo.
6. **Changelog.** Il nuovo testo della politica (`PlatformInstructions.Text` è una costante pubblica: cambio minor) e il
   nuovo default di filum-mcp vanno nel changelog, con come riattivare `full`.

## Verifica di piattaforma

1. Generico: interfaccia e politica, nessun dominio.
2. Sensibilità: invariata; l'output strutturato porta ciò che già portava il testo, contenuto privato solo dove lo
   porterebbe il testo.

## Criteri di accettazione

Gli stessi 6 di `spec.md`, nello stesso ordine.
