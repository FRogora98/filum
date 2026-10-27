# Regole e cose aperte, prese anche quando un turno le perde

> Stato: Implementata (2026-10-19). Rivista (il titolare, 2026-10-19, Traccia A; la cattura non deve dipendere solo dalle
> chiamate durante il turno, nessuno strumento nuovo, il profilo base a 14 strumenti o meno)
> Area: motore (fatti, profilo, consolidamento, istruzioni), filum-mcp (profilo, istruzioni)
> Autore/data: federico rogora (scritta da Claude), 2026-10-19

## Problema

Il turno di riferimento della spec 050 mostra cosa scrive un modello piccolo durante i suoi turni: solo fatti. Non ha
scritto né il core, né la regola ("risposte brevi, a punti"), né la cosa aperta ("ho promesso le foto ad Ana"), neanche
il nome della persona. Una sessione nuova allora infrange la regola e dimentica la promessa:
- cose aperte 0 su 2;
- regole 1 su 3.

C'è anche un buco senza hook. `memory_overview`, la prima chiamata che chiedono le istruzioni, mostra core e mappa ma non
i fatti: per questo la strada senza hook ha fatto 12 su 23.

## Obiettivo

Regole durature e cose aperte si tengono come fatti tipizzati, senza strumenti nuovi. Arrivano subito dopo il core in
ogni profilo. Il consolidamento le prende, insieme a chi è la persona, quando i turni le hanno perse. E la prima chiamata
di una sessione dà lo stesso profilo con o senza hook.

## Comportamento desiderato

1. **Altri due attributi riservati,** oltre ad `alias`:
   - `rule`: un'istruzione duratura, con il suo perché nella nota;
   - `open`: qualcosa di promesso o ancora da fare, con la scadenza nella nota quando c'è.

   Come `alias`, possono avere più valori insieme: un valore nuovo non chiude mai gli altri. Lo stesso valore di nuovo
   non cambia nulla. Una cosa fatta o abbandonata si chiude con `fact_end`.
2. **`fact_end` accetta `value`,** così si chiude uno solo di più valori. Senza, si chiudono tutti i valori aperti
   dell'attributo, come prima.
3. **Il profilo** (spec 045) segue quest'ordine:
   1. core;
   2. **regole** (ciascuna con il suo perché);
   3. **cose aperte** (ciascuna con la scadenza e da quando);
   4. gli altri fatti;
   5. appreso;
   6. mappa;
   7. cosa è stato detto.

   Quando ci sono messaggi in attesa di essere riordinati, finisce con quanti sono, e dice di chiamare
   `memory_consolidate` dopo aver risposto alla prima richiesta.
4. **`memory_overview` restituisce il profilo,** con le skill attive e il prompt di un pacchetto. Così la prima chiamata
   della strada senza hook dà le stesse regole, cose aperte e fatti.
5. **Il consolidamento è l'estrattore garantito.** Le sue istruzioni chiedono:
   - chi è la persona, nel core;
   - le regole durature e le cose aperte, come fatti, da ciò che è stato detto;
   - una cosa aperta chiusa quando gli eventi dicono che è fatta.

   Gira sempre con il modello dell'host, sotto le regole nel codice.
6. **Il profilo base di filum-mcp** offre `memory_consolidate` al posto di `memory_delete`, così la rete di sicurezza c'è
   di default; resta a 14 strumenti. `memory_delete` resta nel profilo completo.
7. **La politica dei fatti** (`InstructionParts.FactPolicy`, condivisa da ogni superficie) aggiunge i due attributi. Le
   istruzioni si accorciano perché entrambi i profili restino sotto i 4.096 caratteri.

## Modificata dopo il primo giro di Track A (2026-10-19)

Il giro ha mostrato che il modello non chiama mai `memory_consolidate` da solo (0 chiamate), quindi la rete di sicurezza
non partiva mai.
- **`filum-mcp tidy`, un hook `Stop`.** Quando aspettano più di 10 messaggi, chiede una volta per sessione, dopo che la
  risposta è stata mostrata, di chiamare `memory_consolidate`. Non lo chiede mai due volte nello stesso turno
  (`stop_hook_active`).
- **Senza hook,** `memory_overview` mostra i messaggi in attesa più recenti, al massimo 1.500 caratteri, e chiede di
  registrare ciò che manca alla memoria.
- **Il profilo core** mantiene un modo per dimenticare: `memory_delete` torna, e `facts_history` passa al profilo full.
- **La spec 050** riporta quale percorso ha scritto ogni elemento, e la latenza e i token della risposta di una
  sessione nuova.

## Modificata dopo il secondo giro di Track A (2026-10-19)

Le regole che `memory_consolidate` dà al modello in filum-mcp erano un elenco più corto di quello di un passaggio
ospitato: non chiedevano né chi è la persona, né le regole, né le cose aperte, quindi il riordino non poteva recuperare
una promessa sfuggita al turno.
- **Ora portano le stesse politiche** (`InstructionParts`): il nucleo, i fatti, regole e cose aperte, ciò che dura.
- **Finiscono con un ultimo controllo:** rileggere gli eventi uno per uno confrontandoli con la memoria: chi è qualcuno,
  un valore che vale ora o è cambiato, una regola, qualcosa di promesso o ancora da fare, qualcosa di fatto che chiude
  una cosa aperta.
- La pagina resta a 16.000 caratteri: nei giri visti ciò che aspettava era molto più corto, quindi una pagina più
  piccola non cambierebbe nulla di misurato.

## Verifica sulla piattaforma

1. Generico: `rule` e `open` non hanno dominio, come `alias`.
2. Sensibilità: invariata; le fonti private non danno citazione (spec 041).

## Criteri di accettazione

1. Due fatti `rule` dello stesso soggetto valgono entrambi, e la stessa regola di nuovo non cambia nulla. `fact_end` con
   `value` chiude una cosa `open` e lascia l'altra.
2. Il profilo elenca le regole (con il perché) e le cose aperte (con la scadenza) subito dopo il core. Nessuna delle due
   ricompare tra gli altri fatti.
3. Con messaggi in attesa, il profilo dice quanti sono e di chiamare `memory_consolidate`. Senza, non ne parla.
4. `memory_overview` restituisce le sezioni del profilo, fatti compresi.
5. Il profilo base di filum-mcp ha 14 strumenti, con `memory_consolidate` e senza `memory_delete`. Entrambi i testi
   delle istruzioni sono sotto i 4.096 caratteri, e la politica dei fatti è in ciascuno di essi e nelle istruzioni del
   consolidamento.
6. La nuova run della spec 050 riporta la cattura separata dal richiamo: per ogni domanda, se ciò che serve è stato
   scritto, se era nel profilo o nel richiamo, e se ha avuto risposta.
