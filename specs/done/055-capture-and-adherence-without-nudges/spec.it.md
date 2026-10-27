# Memorizzare e rispettare le regole senza le spinte dell'host

> Stato: Implementata (2026-10-20)
> Area: motore, MCP
> Autore/data: federico rogora (scritta da Claude), 2026-10-19

## Problema

Le prove isolate del 2026-10-19 hanno mostrato cosa fa Filum quando l'host non spinge il modello verso la memoria. La
memoria automatica di Claude Code, per esempio, lo spinge; l'host di un utente reale può non farlo. Le misure sono i
giri knows-you su tre persone (spec 050) e uno scenario `mcp`. Restano cinque lacune:

- **Memorizzare.**
  - Una promessa o una cosa da fare detta una volta non è stata scritta: "trovare una ditta di traslochi entro fine
    maggio", "iscriversi al comune", "il concerto è andato benissimo".
  - Una lista chiesta dalla persona è stata scritta nella risposta ma non salvata (`claim-trap-it`).
- **Le regole arrivano al modello e vengono ignorate.** "Elenchi brevi", "risposte brevi" e "solo unità metriche"
  erano nel brief e in ogni richiamo, e non sono state seguite in 1 o 2 prove di regola su 3 per persona, con
  Haiku 4.5.
- **Ora e dopo si confondono.**
  - `facts_current` senza `on`, il brief e il richiamo mostrano come validi ora tutti i valori aperti, anche uno che
    comincia nel futuro.
  - Sulla persona 2, un trasloco registrato per luglio ha lasciato la città di maggio senza un valore valido ora.
    Alla domanda "in che città vivo adesso?" il modello ha detto di non saperlo.
- **Astensione.** Alla domanda su una sorella mai nominata, il modello ha indicato la figlia come sorella.
- **Chiusure.** Una cosa aperta a volte viene chiusa su un evento successivo che non la chiude, per esempio un
  passaporto rinnovato preso per un altro.

Il costo del percorso senza hook è una lacuna a parte: circa 1,35 volte i token in ingresso del percorso con gli hook.

## Obiettivo

Una sessione nuova risponde da ciò che è stato detto, segue le regole che la persona ha dato e dice "non lo so" per
ciò che non è mai stato detto. Lo fa solo con istruzioni, brief e richiamo di Filum, su una persona mai vista durante
lo sviluppo.

## Fuori scopo

- Nessuno strumento nuovo, e nessun cambio al formato del file dei fatti.
- Nessuna parola di dominio in codice, prompt o test.
- Nessuna taratura sulle persone congelate 1-3: il verdetto è su una nuova persona tenuta da parte.
- Reranker, embedder e vettori persistenti dei fatti sono altre spec di Track B.

## Comportamento attuale

- `FactService.CurrentAsync` (`src/Filum.Engine/Facts/FactService.cs`) senza `on` tiene ogni fatto aperto e valido,
  qualunque sia il suo `validFrom`.
- Il brief ("Facts that hold now") e il richiamo lo usano (`src/Filum.Engine/Briefing/BriefService.cs`).
- Le regole aprono il brief e si ripetono in ogni richiamo, prima di ciò che corrisponde (spec 045, modificata il
  2026-10-19).
- Il riordino in filum-mcp dà le stesse politiche del passaggio ospitato e un ultimo controllo evento per evento
  (spec 046, modificata il 2026-10-19). Parte solo quando il modello lo chiama, o quando l'hook Stop lo chiede.

## Comportamento desiderato

1. **Ora è oggi.**
   - Senza `on`, "attuale" vuol dire valido oggi, secondo l'orologio dell'host. Un replay (un benchmark) fissa oggi
     esplicitamente, con un parametro o una variabile d'ambiente; oggi non si deduce mai dall'ultimo evento, perché
     una persona che torna dopo tre mesi avrebbe valori vecchi. Ogni benchmark lo fissa al giorno delle sue domande
     (spec 050, `evals/bench`).
   - Un valore che comincia più avanti è mostrato a parte, come previsto: "dal <giorno>: <valore>". Compare nel brief,
     nella risposta di `facts_current` e nel richiamo.
   - Il valore che sostituirà resta mostrato come valido ora.
2. **Le regole, subito prima della risposta.**
   - Il richiamo finisce anche con una riga imperativa che nomina le regole in vigore: "Prima di rispondere, segui: …".
   - Le istruzioni dicono che una regola della persona prevale, per quella risposta, sulle impostazioni predefinite
     dell'host di stile e formato (lunghezza, impaginazione, unità, lingua). Non prevale mai sulla sicurezza né sulle
     regole di sistema dell'host: una memoria che dicesse al modello di scavalcare il suo prompt di sistema sarebbe
     un modo per iniettare istruzioni.
3. **Astensione.** Le istruzioni dicono che ciò che la memoria non contiene è sconosciuto. Non si riempiono mai
   parentela, nome o valore di una persona con qualcos'altro; si dice che non è mai stato detto. Astenersi non deve
   costare risposte vere: un falso "non lo so" su qualcosa che è stato detto si conta, e non deve aumentare.
4. **Memorizzare senza spinte.**
   - Le istruzioni, e l'ultimo controllo del riordino, nominano ciò che va salvato anche quando la persona non dice
     "ricorda": qualcosa di promesso o previsto con una data; qualcosa di fatto che chiude una cosa aperta; una lista
     o un insieme che la persona ha chiesto di fare o tenere.
   - Senza hook, il primo `memory_overview` di una sessione chiede di salvare ciò che la sessione precedente ha
     lasciato non salvato. Il tempo aggiunto alla prima risposta resta entro circa 2 secondi al 95° percentile, e si
     riporta.
5. **Chiusure.** Una cosa aperta si chiude solo con un evento che dice, con parole sue, che è fatta o abbandonata. La
   chiusura conserva l'id dell'evento che la chiude come fonte, come fa un valore registrato, così si può verificare.
6. **Costo senza hook.** In una sessione nuova `memory_overview` dà tutto il brief (al massimo circa 2.000
   caratteri), lo stesso testo che dà l'hook, prima della mappa. Un modello che lo chiama una volta ha ciò che danno
   gli hook, con circa gli stessi token. Un brief parziale si misura solo come ablazione, se il costo si vede.

## Verifica sulla piattaforma

1. Generico: ogni cambiamento riguarda tempo, regole, cose sconosciute e ciò che dura, per qualunque persona e
   dominio.
2. Nulla di privato viene mostrato. Un valore previsto segue la sensibilità del file dei fatti come uno attuale.
   Nessun dato personale entra nel repository: i test usano persone sintetiche.

## Criteri di accettazione

1. Dato un fatto che comincia domani e il valore che sostituisce, quando brief, `facts_current` e richiamo girano
   oggi, allora il valore sostituito risulta valido ora e quello nuovo previsto dal suo giorno. Test unitario.
2. Dato un fatto di regola, quando il richiamo gira su qualunque messaggio, allora il suo testo finisce con la riga
   "Prima di rispondere, segui"; e le istruzioni limitano la portata di una regola a stile e formato. Test unitari.
3. Data una cosa aperta chiusa dal riordino, allora la chiusura contiene l'id dell'evento che la chiude; e le regole
   del riordino richiedono le parole di quell'evento. Test unitario; l'effetto si misura al punto 5.
4. Date le istruzioni e le regole del riordino, allora nominano promesse, chiusure e liste chieste come cose da
   salvare senza che venga chiesto. Test unitario.
5. Data una quarta persona sintetica, scritta e congelata prima di qualsiasi prova su di lei, quando knows-you gira
   con Haiku 4.5 (con e senza hook) e gpt-4o-mini (con hook) sulle prove isolate, allora:
   - Haiku con gli hook fa almeno 20 su 23, e senza hook almeno 17;
   - le prove di regola con gli hook passano almeno 2 su 3;
   - le prove di astensione passano;
   - le persone 1-3 vengono rifatte e riportate accanto, senza perdere più di 1 domanda per variante, e senza più
     falsi "non lo so" su cose che sono state dette;
   - senza hook, il tempo aggiunto alla prima risposta si riporta rispetto al limite di 2 secondi;
   - gpt-4o-mini con gli hook si riporta rispetto al suo intervallo precedente (da 16 a 19), senza soglia.

   La persona 4 contiene, scritti prima di qualsiasi prova: almeno 2 prove di astensione, almeno 1 prova di un
   cambiamento previsto rispetto al valore attuale, almeno 1 trappola di chiusura (un evento che sembra "fatto" e non
   lo è), e almeno 1 lista chiesta dalla persona.
6. Data la suite di scenari `mcp` sulle prove isolate, quando gira prima e dopo, allora gli scenari che controllano il
   salvataggio, `claim-trap-it` compreso, non peggiorano. Si riportano entrambi i giri.

## Domande aperte

Nessuna: entrambe hanno avuto risposta nella revisione (2026-10-19). "Oggi" è l'orologio dell'host, con una
sovrascrittura esplicita per i replay (punto 1). La prima panoramica di una sessione nuova dà tutto il brief (punto 6).
