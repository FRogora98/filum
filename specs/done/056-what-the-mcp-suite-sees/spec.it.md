# Cosa vede la suite mcp, le skill su richiesta e le regole in una chat nuova

> Stato: Implementata (2026-10-21; criterio 6 non raggiunto, vedi tasks.md)
> Area: motore, MCP, eval
> Autore/data: federico rogora (bozza di Claude), 2026-10-21

## Problema

Sei scenari della suite `mcp` falliscono in ogni prova sul banco isolato, prima e dopo la spec 055 (la prova del
2026-10-20): `count-sum`, `rule-follow`, `skill-command-it`, `skill-create-use`, `skill-propose-it` e `skill-starter`.
Le risposte e le chiamate ai tool (quella prova, `with filum`, Haiku 4.5) mostrano due tipi di causa.

**La suite non riesce a vedere un successo (banco di prova).**
- **Ogni turno è un nuovo `claude -p`.** I turni della stessa chat non proseguono. "Sì, salvala" dopo una proposta
  arriva a un modello che la proposta non l'ha mai vista (`skill-propose-it`, turno 4: "I don't have context").
- **Le chiamate diventano passi con una sola regola:** un tool che scrive è "wrote", tutto il resto è "read". Quindi:
  - `collection_aggregate` e `compute_run` non sono mai un passo calcolato (`count-sum` turno 2 non può passare);
  - `skill_use` non è mai "Used skill /nome" (`skill-starter` ha usato la skill e risposto giusto, e fallisce lo
    stesso);
  - `skill_propose` non produce mai una proposta (`skill-propose-it` turno 3 non può passare);
  - `fact_end` non conta come scrittura.
- **`rule-follow` controlla la sezione Rules del file core.** In filum-mcp una regola è un fatto di tipo regola
  (spec 045); il modello l'ha salvata lì con `fact_record` e il controllo è fallito.

**Filum fa la cosa sbagliata (prodotto).**
- **Una skill chiesta per nome viene solo proposta.** "Create a skill called log-note: …" ha portato a
  `skill_propose` e "Would you like me to save this skill?": niente salvato, e nelle chat dopo `/log-note` non c'era.
  In `skill-command-it` la risposta diceva "Fatto!" senza aver salvato niente: un'affermazione falsa.
- **Una chat nuova senza chiamate ai tool non vede mai le regole.** Senza hook, "What is the capital of Portugal?" ha
  avuto risposta senza nessuna chiamata, quindi la regola "chiudi ogni risposta con 'Onward.'" non è mai arrivata al
  modello. Il brief arriva solo tramite `memory_overview`, che il modello salta quando la domanda sembra generica.
- **I totali si fanno leggendo.** `count-sum` ha dato giusti 3 corse e 25,5 km, ma da `memory_read`, non da
  `collection_aggregate`. Su tre righe va bene; su trecento no.

## Obiettivo

La suite `mcp` misura quello che dice di misurare. Sistemato questo: una skill chiesta per nome viene salvata e
funziona per nome in una chat nuova; una regola arriva in una chat nuova anche se il modello non chiama nessun tool;
conteggi e totali vengono da un calcolo.

## Fuori obiettivo

- Nessun tool nuovo e nessun cambio ai formati dei file.
- Il flusso dell'app ospitata resta: lì una skill suggerita da Filum si mostra come proposta e si salva solo col sì.
  Questa spec cambia solo il caso in cui è la persona a chiedere la skill.
- Nessuna parola di dominio in codice, prompt o test.
- Il percorso con gli hook non cambia: con gli hook il brief porta già le regole.

## Comportamento attuale

- `ClaudeCodeAgentStrategy` (`src/Filum.Evals/Mcp/`) avvia un nuovo `claude -p` per ogni turno, qualunque sia la
  chat.
- `AgentToolSteps.Of` trasforma ogni nome di tool in un passo "wrote" o "read", con descrizione "Called <tool>".
- Le istruzioni MCP (`src/Filum.Mcp/McpInstructions.cs`) sulle skill dicono: "quando chiede di nuovo lo stesso tipo
  di cosa, offrine una, e salvala con skill_save solo se è d'accordo". Non dicono niente di una skill chiesta dalla
  persona.
- Le istruzioni del server sono lo stesso testo per tutti; le regole arrivano al modello solo tramite
  `memory_overview`, l'hook del brief o quello del richiamo.

## Comportamento desiderato

1. **Le chat proseguono.** I turni di una chat girano in una sola sessione di Claude Code (`--resume` con l'id di
   sessione di quella chat); una chat nuova apre una sessione nuova.
2. **I passi come li riporta l'host.**
   - `collection_aggregate` e `compute_run` sono passi calcolati.
   - `skill_use` è "Used skill /<nome>", col nome con cui è stato chiamato.
   - `skill_propose` dà al turno la sua proposta, letta dall'input del tool.
   - `fact_end` è una scrittura.
3. **Una regola si trova dove filum-mcp la tiene.** Un controllo `core_contains` passa quando il testo è nel file
   core o in una regola valida nel file dei fatti; anche `core_not_contains` guarda in entrambi. Nome e significato del controllo ("la memoria tiene questa
   regola") restano.
4. **Una skill chiesta si salva.** Le istruzioni dicono: quando la persona chiede una skill (per nome, o "fammi una
   skill che …"), salvala con `skill_save` e di' che è salvata; proponila con `skill_propose` solo quando l'idea è di
   Filum. Mai dire che una skill è salvata prima che `skill_save` abbia risposto.
5. **Le regole nelle istruzioni del server.** All'avvio filum-mcp aggiunge alle sue istruzioni le regole valide oggi:
   al massimo 800 caratteri e solo quanto entra sotto il limite di 4.096 caratteri insieme a tutto il resto (circa 300
   col profilo completo, per questo le istruzioni sono state accorciate), le più recenti prima, nessuna se il file dei
   fatti è privato. Il resto resta in `memory_overview`. Una chat nuova ha così le regole anche se il modello non chiama nessun tool. Una regola
   data durante la sessione arriva al modello col richiamo e il brief, come ora.
6. **Conteggi e totali col calcolo.** Le istruzioni dicono che un conteggio, una somma o una media sulle righe viene
   da `collection_aggregate` (o `compute_run`), mai dal sommare a mano quello che si è letto.

## Controllo di piattaforma

1. Generico: chat, passi, skill, regole e calcolo, per qualunque persona e dominio.
2. Niente di privato: con il file dei fatti privato, nessuna regola va nelle istruzioni del server. Le istruzioni arrivano già nel
   contesto dell'host, come il brief; questo anticipa solo le regole. I test usano persone sintetiche.

## Criteri di accettazione

1. Dati due turni di una chat e uno di un'altra, quando la suite li esegue, i primi due condividono una sessione di
   Claude Code e il terzo no. Test unitario sugli argomenti.
2. Date chiamate a `collection_aggregate`, `compute_run`, `skill_use`, `skill_propose` e `fact_end`, i loro passi
   sono calcolato, "Used skill /<nome>", una proposta e una scrittura. Test unitari.
3. Data una regola tenuta come fatto, `core_contains` con le sue parole passa. Test unitario.
4. Data una home con una regola, quando filum-mcp parte, le sue istruzioni la contengono; con il file dei fatti
   privato no; e le istruzioni del profilo completo restano sotto 4.096 caratteri anche con molte regole. Test.
5. Date le istruzioni, dicono che una skill chiesta si salva con `skill_save` e che i totali vengono da un calcolo.
   Test unitario.
6. Data la suite `mcp`, isolata, Haiku 4.5, 3 ripetizioni, solo `with filum`:
   - gira una volta con le correzioni del banco (1-3) sul motore di oggi, e una volta con tutto;
   - dopo tutto, ognuno dei sei scenari passa in almeno 2 ripetizioni su 3;
   - gli altri undici scenari perdono al massimo 1 successo in tutto, e le affermazioni false non aumentano;
   - si riportano entrambe le prove. Circa 4 $ eq. di quota in tutto; niente soldi veri.

## Decisioni (erano domande aperte)

1. **Le regole nelle istruzioni (punto 5).** Arrivano in ogni chat, anche in una che parla d'altro. L'alternativa è
   lasciarle solo in `memory_overview` e accettare che un modello che non chiama tool non le veda. Deciso: nelle
   istruzioni, perché la persona le ha date per ogni risposta.
2. **L'app ospitata.** Anche l'agente ospitato dovrebbe salvare subito una skill chiesta per nome, invece di
   proporla? Deciso: non in questa spec. Il flusso ospitato resta (il code freeze); sarebbe un cambio di piattaforma a sé.
