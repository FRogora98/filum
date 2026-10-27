# Un benchmark che chiunque può lanciare

> Stato: Implementata (2026-10-18). Rivista (il titolare, 2026-10-18, lacuna 1 dell'audit del motore, passo C5 del piano)
> Area: valutazione (`evals/bench/`, `src/Filum.Evals`)
> Autore/data: federico rogora (scritta da Claude), 2026-10-18

## Problema

I numeri di Filum su FactConsolidation di MemoryAgentBench e su LongMemEval vengono da un harness Python che non è mai
stato in questo repository, e che guida un agente tramite un abbonamento personale:
- **Un esterno non può lanciarlo.** Gli script non sono qui.
- **I costi non sono dollari.** Le cifre di un abbonamento sono "dollari equivalenti", non quanto pagherebbe una chiave
  API.
- **Il runner C# non li riproduce.** `src/Filum.Evals` è un secondo harness che non ha prodotto nessuno dei numeri
  principali.

Mem0, Supermemory, LongMemEval e MemoryAgentBench pubblicano tutti harness che un esterno può lanciare.

## Obiettivo

Un solo comando, con la chiave API dell'utente, costruisce la memoria di Filum dalla storia di un benchmark e risponde
alle sue domande. I bracci sono Filum e i riferimenti con cui è stato confrontato. Il comando scrive:
- un risultato per domanda;
- un report con il test appaiato;
- i dollari reali spesi, entro un tetto dato prima.

## Fuori scopo

- Nessun benchmark nuovo: FactConsolidation (MemoryAgentBench) e LongMemEval, come sono stati lanciati.
- Nessun braccio Mem0 o Graphiti: richiedono stack e chiavi propri (una spec successiva potrà aggiungerli).
- Mai in CI né in `dotnet test`: la logica dell'harness ha test unitari, le run si lanciano a mano.
- Nessun cambio al motore né a `filum-mcp`.

## Comportamento attuale

- `src/Filum.Evals`: run di scenari e LongMemEval su un host in esecuzione, o su Claude Code.
- Gli script dietro i numeri pubblicati sono altrove.
- Il passo che scrive una storia di LongMemEval in una cartella di memoria ("preload") è un programma a parte, anche lui
  altrove.

## Comportamento desiderato

1. **`evals/bench/`** è un piccolo harness Python (MIT, Python 3.11 o successivo; `requirements.txt` con `openai`,
   `pandas`, `pyarrow`). È un solo comando, `python evals/bench/bench.py <benchmark> [opzioni]`, che fa ogni passo e
   riprende da dove si è fermato:
   1. **Scarica i dati** da URL fissati. Ogni file si usa solo se il suo SHA-256 corrisponde.
   2. **Costruisce `filum-mcp`** da questo repository con `dotnet publish`, o usa `--filum-mcp`.
   3. **Costruisce ogni memoria:**
      - LongMemEval: ogni turno come evento (punto 5), poi un agente salva i fatti.
      - FactConsolidation: un agente salva ogni fatto.
   4. **Risponde con ogni braccio**, ciascuno con lo stesso modello.
   5. **Giudica.** LongMemEval: i prompt degli autori. FactConsolidation: la metrica di sottostringa degli autori.
   6. **Scrive i risultati.**
2. **Backend.**
   - `--backend openai` (il default) è qualunque endpoint compatibile con OpenAI (OpenAI, OpenRouter, un server
     locale), con `OPENAI_API_KEY` e un `OPENAI_BASE_URL` facoltativo.
     - I bracci di Filum raggiungono `filum-mcp` via stdio con un client MCP minimo: le sue istruzioni come prompt di
       sistema, i suoi strumenti come funzioni.
   - `--backend claude-code` è `claude -p`, come sono state fatte le run pubblicate.
   - La chiave si legge solo dall'ambiente. Non viene mai stampata, scritta né registrata.
3. **Bracci.**
   - **LongMemEval:**
     - `filum-facts`: i turni come eventi più i fatti salvati da un agente;
     - `filum`: solo i turni come eventi;
     - `bm25`: le cinque sessioni che BM25 mette in cima, nel prompt.
   - **FactConsolidation:**
     - `filum-facts`: i fatti salvati uno per uno da un agente, con l'istruzione esplicita delle run pubblicate;
     - `bm25`: i dieci blocchi che BM25 mette in cima;
     - `long-context`: tutta la storia nel prompt.
4. **Soldi.**
   - Il costo di ogni chiamata viene dal suo uso e dai prezzi del modello: una piccola tabella per i modelli noti, o
     `--price-in` e `--price-out` in USD per milione di token. Un modello senza prezzo noto viene rifiutato.
   - Prima di partire, la run stima il proprio costo e rifiuta di superare `--cap` (USD) senza `--force`.
   - Durante la run, prenota il caso peggiore di ogni chiamata prima di mandarla e si ferma prima che il tetto possa
     essere superato.
   - Su `claude-code` le cifre sono i dollari equivalenti che riporta Claude Code, e sono etichettate così.
5. **Preload in C#.** `dotnet run --project src/Filum.Evals -- preload` scrive le storie di LongMemEval in cartelle di
   memoria:
   - ogni sessione della storia è una conversazione;
   - ogni turno è un evento datato dalla sua sessione: quelli della persona `said`, quelli dell'assistente `answered`;
   - i vettori di ricerca si fanno con il modello locale quando c'è.
   È il programma che hanno usato i turni pubblicati su LongMemEval.
6. **Risultati.** `<out>/results.jsonl` ha una riga per braccio e domanda:
   - la domanda, la risposta attesa e la risposta;
   - se è corretta;
   - le chiamate agli strumenti;
   - token, costo, secondi e i modelli (risposta e giudice).

   `<out>/report.md` dà:
   - l'accuratezza di ogni braccio, per abilità (LongMemEval) o per salto (FactConsolidation);
   - il test esatto di McNemar per ogni coppia di bracci sulle stesse domande;
   - il costo.
7. **Domande.**
   - `--ids <file>`: un elenco di id o indici di domande.
   - `--count <n> --seed <s>`: una selezione stratificata per tipo in proporzione all'insieme, ordinata da un hash del
     seme, come le estraevano i turni pubblicati.
   - La selezione si scrive in `<out>`, così una run dice cosa ha chiesto.

## Verifica sulla piattaforma

1. Generico: i benchmark sono pubblici e senza dominio, e l'harness non nomina nessuna persona.
2. Non si pubblica nulla di personale:
   - i dati sono quelli dei benchmark;
   - cartelle di memoria e risultati restano dove puntano `--work` e `--out`;
   - nessuna chiave viene scritta da nessuna parte.

## Criteri di accettazione

1. Dato un file scaricato il cui SHA-256 non corrisponde, la run si ferma e dice quale file.
2. Dato `--cap 1` e una stima sopra, la run rifiuta di partire e stampa la stima; con `--force` parte, e si ferma lo
   stesso prima che la spesa superi il tetto.
3. Dato un modello senza prezzo noto e senza `--price-in`/`--price-out`, la run rifiuta di partire.
4. Data una run fermata, rilanciata con lo stesso `--out`, le domande già risposte non si richiedono e il loro costo
   conta contro il tetto.
5. Il ranking BM25, la divisione in blocchi, i prompt del giudice di LongMemEval, la metrica di sottostringa e la
   selezione stratificata danno gli stessi risultati delle run pubblicate su input fissi (test unitari con i valori
   delle run pubblicate).
6. Il test esatto di McNemar dà i p pubblicati sui loro conteggi: 23 a 8 dà p 0,011, e 6 a 3 dà p 0,51.
7. Il ponte MCP elenca gli strumenti di `filum-mcp` e ne chiama uno, su una build di questo repository (un test
   d'integrazione che non richiede chiavi).
8. `preload` scrive le sessioni di una storia come conversazioni di eventi `said` e `answered`, ciascuno datato dalla
   sua sessione e nell'ordine del file, come li scrivevano i turni pubblicati (un test su un elemento sintetico di due
   sessioni).
9. Il README dice come lanciare ogni benchmark con un comando, quanto costa, e cosa devia dal protocollo di ciascun
   benchmark (il modello giudice, l'istruzione esplicita).

## Domande aperte

Nessuna: il titolare ha fissato i requisiti (la chiave dell'utente, un risultato per domanda, un comando), e le
scelte tecniche sono del motore.
