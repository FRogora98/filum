# Ti conosce dal primo messaggio

> Stato: Implementata (2026-10-19). Rivista (il titolare, 2026-10-19: "figo cosi, mi piace, vai parti pure", dopo il confronto con un agente
> personale che tiene la sua memoria in file)
> Area: motore (profilo e richiamo), filum-mcp (istruzioni, l'hook `log`)
> Autore/data: federico rogora (scritta da Claude), 2026-10-19

## Problema

Un agente con `filum-mcp` non sa nulla della persona finché non chiama uno strumento. Le istruzioni dicono di chiamare
memory_overview quando si inizia ad aiutare; un modello che non lo fa, o lo fa tardi, risponde al primo messaggio come a
uno sconosciuto. Poi ricorda solo quando decide di cercare (`events_search`, `facts_current`). Un turno in cui non pensa
di cercare perde ciò che la memoria contiene.

Un agente personale che sembra conoscere la sua persona lo fa senza meccanismi complicati. Un piccolo profilo (chi è,
le sue regole, dove stanno le cose) è nel contesto prima del primo turno, e il modello sa sempre che la memoria esiste.

## Obiettivo

Dal primo messaggio di ogni sessione l'agente sa chi è la persona e cosa contiene la memoria, senza chiamare strumenti.
A ogni messaggio, ciò che la memoria contiene su quel messaggio arriva all'agente anche se non cerca.

## Fuori scopo

- Nessuna chiamata a un modello dentro Filum: profilo e richiamo sono deterministici (il motore resta senza modello).
- Nessun cambio agli strumenti né al loro catalogo.
- Cose aperte, regole con il loro perché e una valutazione del "sa chi sei" sono spec successive.

## Comportamento desiderato

1. **Un profilo.** `IMemoryService.BriefAsync(userId, maxChars)` dà un testo di al massimo `maxChars` (default
   `MemoryOptions.BriefChars`, 8.000 caratteri, circa 2.000 token). Le sezioni vengono in quest'ordine, ciascuna
   tagliata alla sua quota, e una sezione senza niente da dire si omette:
   1. **Il core della persona** (`/filum.md`): chi è, le sue regole, come vuole le risposte.
   2. **Ciò che è stato appreso** (`/learned.md`): le sue righe più recenti.
   3. **I fatti validi ora**: prima i registrati più di recente, uno per riga, poi quanti altri ce ne sono.
   4. **Cosa contiene la memoria**: la mappa dei file, uno per riga.
   5. **Cosa è stato detto**: quanti messaggi sono tenuti, da quando a quando, e che `events_search` li trova.

   Non contiene nulla di privato.
2. **`filum-mcp brief`, un hook per l'inizio della sessione.**
   - Scrive il profilo come risposta dell'hook `SessionStart` di Claude Code (`hookSpecificOutput.additionalContext`),
     così la persona è nel contesto prima del primo messaggio, senza chiamare strumenti.
   - Il profilo non va nelle istruzioni MCP: Claude Code taglia le istruzioni dei server a 4.096 caratteri, e quelle di
     filum-mcp sono già 3.098 (profilo base) e 4.075 (tutti gli strumenti).
   - Un test tiene le istruzioni dei due profili sotto i 4.096 caratteri.
   - Gli host senza hook mantengono il primo passo delle istruzioni, `memory_overview`.
   - Come `log`, non fa mai fallire una sessione: a ogni errore non scrive nulla ed esce con 0.
3. **Un richiamo.** `IMemoryService.RecallAsync(userId, message, maxChars)` dà ciò che la memoria contiene su un
   messaggio, al massimo `maxChars` (default `MemoryOptions.RecallChars`, 1.500 caratteri):
   - i fatti che possono rispondere (il confronto della spec 042, solo parole);
   - le poche cose dette che corrispondono meglio, ciascuna tagliata corta, con data e numero dell'evento.

   È vuoto quando nulla corrisponde. Non contiene mai contenuto privato.
4. **L'hook `log` risponde con il richiamo.**
   - `filum-mcp log` (l'hook `UserPromptSubmit` di Claude Code) continua a tenere il messaggio nel log.
   - Ora risponde anche all'hook con il richiamo del messaggio, come contesto aggiunto. Il richiamo si cerca prima di
     tenere il messaggio, così il messaggio non richiama se stesso.
   - Un input in testo semplice (un altro host) non riceve risposta, come prima.
   - Un errore non blocca mai il messaggio: non si risponde nulla, e il motivo va su stderr.
5. **Il README** mostra i due hook (`SessionStart` con `filum-mcp brief`, `UserPromptSubmit` con `filum-mcp log`), e
   dice cosa riceve ogni host: Claude Code con gli hook riceve la persona all'avvio e il richiamo a ogni messaggio; gli
   altri host ricevono la memoria tramite `memory_overview` e gli strumenti, come prima.

## Modificata dopo il secondo giro di Track A (2026-10-19)

Sulla prima persona, con Haiku 4.5 e gli hook, regole presenti in memoria non sono state seguite, e una sessione senza
hook ha risposto dall'account dell'host ("vedo la tua email") invece che dalla memoria.
- **Il brief comincia dalle regole,** sotto "seguile tutte in questa risposta", prima del nucleo.
- **Ogni richiamo ripete le regole in vigore,** entro il 40% del suo budget, prima di ciò che corrisponde al messaggio;
  anche un messaggio che non corrisponde a nulla le riceve.
- **Il brief e le istruzioni dicono che la memoria è la fonte sulla persona:** prevale su qualunque altra cosa il
  contesto dica di lei.

## Verifica sulla piattaforma

1. **Generico:** qualunque memoria e persona. Sezioni e ordinamento non nominano nessun dominio.
2. **Sensibilità:**
   - file, fatti ed eventi privati non entrano mai nel profilo né nel richiamo;
   - profilo e richiamo sono in sola lettura, e non cambiano nulla.

## Criteri di accettazione

1. Data una memoria con core, righe apprese, fatti, file e messaggi, il profilo ha le cinque sezioni in ordine. È al
   massimo `maxChars`, e il core viene per primo e intero quando ci sta.
2. Dati più fatti di quanti ne stanno, il profilo mostra i registrati più di recente e dice quanti altri valgono.
3. Dati file e messaggi privati, nessuno dei due è nel profilo né nel richiamo.
4. Dati un fatto e un messaggio che corrispondono a una domanda, il richiamo mostra entrambi. Data una domanda che non
   corrisponde a nulla, è vuoto.
5. `filum-mcp brief` scrive la risposta dell'hook con il profilo della sua memoria. Con una cartella rotta esce con 0
   senza nulla su stdout. Le istruzioni di filum-mcp sono sotto i 4.096 caratteri in entrambi i profili (un test).
6. `filum-mcp log` con il JSON di un hook tiene il messaggio. Scrive su stdout la risposta dell'hook con il richiamo
   (`hookSpecificOutput.additionalContext`), che non contiene il messaggio stesso. Con un input in testo semplice non
   scrive nulla su stdout. Con una cartella rotta esce con 0 senza nulla su stdout.
7. Lo snapshot del catalogo degli strumenti non cambia.
