# Turni leggeri: i fatti che cambiano letti bene, a un costo minore per messaggio

> Stato: Rivista (il proprietario ha delegato le spec dopo la 030, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (strumenti, istruzioni della piattaforma) / il turno
> Autore/data: federico rogora (redatta da Claude), 2026-10-02
> Roadmap: fase E, dopo la 030; dalla sua misura (`evals/reports/20261002-170349/`)

## Problema

La misura della spec 030 ha lasciato due lacune.

- **I fatti che cambiano.** Gemma ha risposto giusto a 2 domande di aggiornamento su 4 di `oracle`.
  - "Dove tenevo all'inizio le mie scarpe vecchie?" ha avuto come risposta il posto successivo.
  - A una collezione cresciuta di uno ("ho appena aggiunto una moneta", prima erano 37) ha risposto con un conteggio di righe invece che con 38.

  In entrambi i casi l'agente ha risposto con la prima cosa trovata, senza controllare come era cambiato il valore: né `facts_history` né quello che era stato detto.
- **Il costo per messaggio.**
  - Un turno manda circa 19.000 token di input. La parte fissa viene rimandata a ogni chiamata al modello del turno, e un turno ne fa tre o quattro: le descrizioni e gli schemi dei 26 strumenti (circa 14.500 caratteri, 3.600 token) e le istruzioni della piattaforma (circa 1.100 token).
  - La cache dei prompt oggi non aiuta: tramite OpenRouter questi modelli sono serviti da fornitori diversi, e nessuno ha riportato un token in cache su un prefisso ripetuto (prova del 2026-10-02).
  - I prezzi del catalogo sono più bassi di quanto fanno pagare alcuni fornitori (Gemma: 0,09 contro 0,15 USD per milione di token di input presso uno di loro), quindi il costo riportato è ottimistico.

## Obiettivo

L'agente legge nel modo giusto i fatti che cambiano, e un turno manda meno, senza perdere nessuna capacità.

## Non obiettivi

- **Nessun cambio a quello che la memoria tiene** (spec 030) né al comportamento degli strumenti: solo le loro descrizioni, le istruzioni, e quali strumenti offre un host.
- **Niente lavoro su fornitori fissi o cache:** un host può configurare i suoi fornitori; questa spec misura, non instrada.
- **Niente costo come addebitato:** registrare quello che riporta il fornitore invece della stima del catalogo resta per dopo; i report continuano a dire che il costo è stimato.
- **Nessuna messa a punto per domanda:** ogni regola è generica, scritta per qualunque persona e dominio, mai per le parole di un benchmark.

## Comportamento attuale

- `PlatformInstructions.Text` dice quando registrare i fatti, non come rispondere su di loro.
- Le descrizioni degli strumenti (`MemoryTools`) sono scritte per un modello senza altre indicazioni, e ripetono quello che dicono le istruzioni.
- Il turno ospitato offre `memory_overview`, anche se le sue istruzioni contengono già il nucleo, la mappa e le skill.

## Comportamento desiderato

1. **Leggere quello che cambia.** Le istruzioni dicono all'agente, in modo generico:
   - a una domanda su cosa vale ora, cosa valeva prima o all'inizio, da quando, o quanti, si risponde da `facts_current` / `facts_history`, verificando con `events_search` quando i fatti potrebbero mancare;
   - quando la persona dice che un valore è cambiato di una quantità ("uno in più", "due in meno"), il nuovo valore si calcola dall'ultimo e si registra con `fact_record`.
2. **Strumenti più corti.** Ogni strumento tiene nome, parametri e comportamento. La sua descrizione e quelle dei parametri dicono cosa fa e niente di quello che dicono già le istruzioni. L'intero catalogo è al massimo di 10.000 caratteri come lo riceve il modello (da circa 14.500; circa 6.500 sono la struttura degli schemi, che nessuna formulazione accorcia).
3. **Nessuno strumento che al turno non serve.** Il turno ospitato non offre `memory_overview`, il cui contenuto è già nelle istruzioni. `filum-mcp` lo tiene.

## Verifica di piattaforma

1. **Generico:** sì. Le regole parlano di valori che cambiano, mai di un dominio.
2. **Sensibilità:** invariata. Nessuno strumento perde le sue regole di sensibilità, e il repository pubblico non riceve niente di personale.

## Criteri di accettazione

1. Dato il catalogo, quando viene serializzato come lo riceve il modello, allora è al massimo di 10.000 caratteri, ogni strumento ha nome, parametri e una descrizione, e lo snapshot mostra il cambiamento.
2. Dato un turno ospitato, quando vengono offerti gli strumenti, allora `memory_overview` non c'è; in `filum-mcp` c'è.
3. Date le istruzioni, allora contengono le regole del comportamento desiderato 1, e gli scenari di eval del motore restano verdi sul modello predefinito.
4. Dato LongMemEval `oracle` (le 20 domande della spec 030) su Gemma 4 31B, quando viene rigiocato, allora gli aggiornamenti sono almeno 3 su 4, l'accuratezza non è sotto l'85%, e i token di input per domanda sono meno che nel report della spec 030.

## Domande aperte

Nessuna: il proprietario ha delegato le decisioni. La misura (criteri 3 e 4) costa circa 0,60 $ e richiede il sì del proprietario prima di partire.
