# Contare attraverso le conversazioni

> Stato: Bozza
> Area: motore, MCP
> Autore/data: federico rogora (bozza di Claude), 2026-10-27

## Problema

Su LongMemEval_S le domande che uniscono più conversazioni sono il gruppo di errori più grande. Su quattro turni e 541
domande, la memoria a fatti ha risposto bene a 82 delle 127 domande multi-sessione (BM25 sulle cinque sessioni migliori:
76). Dei suoi 45 errori, circa quattro su cinque sono conteggi o totali di cose dette in conversazioni diverse: "quanti
mobili ho comprato, montato, venduto o riparato", "quanto ho speso in articoli di lusso", "quanti fratelli ho". Il
modello trova due o tre cose su quattro e conta quelle.

La causa è nella ricerca, non nei conti del modello:
- `events_search` dà al massimo 8 messaggi (`MemoryOptions.EventsSearchMax`), poi le conversazioni migliori entro un
  budget. Va bene per una domanda con una risposta, non per un elenco le cui voci sono dette in otto conversazioni.
- Una ricerca usa un solo gruppo di parole. "Mobili" non trova "libreria" o "divano": un conteggio ha bisogno della
  categoria e dei suoi membri.
- Niente dice al modello che contare è un lavoro diverso dal cercare: somma quello che ha letto e si ferma presto.

## Obiettivo

Una domanda che conta o somma cose dette in più conversazioni ha risposta da ogni voce detta, ciascuna col giorno e la
conversazione in cui è stata detta, contata una volta.

## Fuori obiettivo

- Nessun cambio a come si scrive una memoria, al file dei fatti o al formato della cartella.
- Nessuna parola di dominio in codice, prompt o test: "voci", "una categoria e i suoi membri".
- Nessuna messa a punto sulle domande che la misura usa (vedi criterio 4).
- Il ragionamento temporale (aritmetica sulle date) è un'altra causa e un'altra versione.
- Nessuno scambio automatico: il motore non indovina che una domanda conta; è il modello a chiedere l'elenco
  (deterministico e verificabile).

## Comportamento attuale

- `events_search(query, from, to, includePrivate)` ordina i messaggi per parole e, col modello di embedding, per
  significato, tiene i migliori 8 (`EventService`, `EventSearch.Fuse`), e mostra i fatti che possono rispondere e le
  conversazioni migliori entro `RecallBudgetChars` (`EventTools.SearchEvents`, `RecallPage`).
- Le istruzioni dicono come cercare una cosa; non dicono niente sul contare.

## Comportamento desiderato

1. **Una ricerca che elenca ogni menzione.** `events_search` riceve `every` (falso di default). Con esso, la ricerca dà
   ogni messaggio che corrisponde, fino a `MemoryOptions.EventsListMax` righe (nuovo, 100 di default) e
   `EventsListChars` caratteri (nuovo, 16.000 di default), in ordine di giorno, una riga ciascuno: numero, giorno, chi
   l'ha detto e un estratto di circa 160 caratteri centrato sulla corrispondenza (non l'inizio del messaggio, che
   perderebbe la voce o il numero), e una riga finale con quanti ne ha trovati e, se ha raggiunto un limite, che
   potrebbero essercene altri.
   Nessuna conversazione mostrata intera: il modello ne legge una con `events_read` quando una riga non basta. Le stesse
   regole sui messaggi privati di una ricerca normale.
2. **Più parole in una volta.** La `query` di una ricerca a elenco può contenere più alternative separate da `|` (una
   categoria e i suoi membri: "mobili | libreria | divano | scrivania"); un messaggio corrisponde se ne contiene una, ed
   è elencato una volta.
3. **Le istruzioni dicono come contare.** Per "quanti", "quanto in totale" o "quale di più": elenca con `every`, con la
   categoria e i suoi membri probabili; tieni ogni voce distinta una volta (la stessa cosa detta due volte è una), col
   suo giorno; rispetta il periodo che la domanda nomina; poi conta o somma con `compute_run` quando è offerto, e di' le
   voci nella risposta.
4. **Un suggerimento quando una ricerca è tagliata.** Una ricerca normale che aveva più corrispondenze di quelle che
   mostra finisce con una riga: altri messaggi corrispondono; per contarli o sommarli, elencali con `every`.
5. **L'API del motore:** `IMemoryEvents.ListEventsAsync(userId, query, from, to, includePrivate, max)` dà l'elenco,
   così anche un host diverso da filum-mcp può usarlo.

## Controllo di piattaforma

1. Generico: elencare ogni menzione e contare le voci distinte vale per qualunque persona e dominio.
2. Niente di privato compare se non chiesto, esattamente come in una ricerca normale. Un elenco è limitato
   (`EventsListMax` righe di circa 160 caratteri), quindi non può inondare un contesto.

## Criteri di accettazione

1. Dati 30 messaggi che nominano una voce in 12 conversazioni, quando `events_search` gira con `every`, allora tutti e
   30 sono elencati in ordine di giorno coi loro numeri, una volta ciascuno, entro il limite; oltre il limite, la riga
   finale lo dice. Test unitari sugli store in memoria e a cartella.
2. Data una query "a | b", allora un messaggio che dice solo "b" è elencato, e uno che dice entrambi è elencato una
   volta. Test unitario.
3. Date le istruzioni, allora nominano `every` per conteggi e totali, le voci distinte, il periodo e `compute_run`, e
   le istruzioni del profilo completo restano sotto 4.096 caratteri. Test unitario.
4. **La misura** (LongMemEval_S, le memorie a fatti già costruite, Haiku 4.5 col banco isolato, giudicate come prima):
   - sviluppo solo sulle 200 domande del primo turno (seed `5c-20261017`);
   - spec e codice si congelano, poi il set di test gira una volta: le domande degli altri turni (circa 290), con due
     bracci sulle stesse memorie, questa versione e la 1.0.0;
   - **passa** se, sulle domande multi-sessione del set di test, questa versione risponde bene ad almeno 8 domande più
     della 1.0.0 con McNemar esatto p < 0,05; tutte le domande insieme non perdono più di 2 punti percentuali; e nessuna
     categoria perde più di 3 risposte nette (soprattutto quelle su una sola sessione, dove un elenco non aggiunge niente);
   - durante lo sviluppo si riporta quanto spesso il modello elenca su una domanda di una sola sessione: non dovrebbe;
   - limiti dichiarati: gli errori del set di test sono stati intravisti (un loro elenco è stato letto una volta prima
     di questa spec); le sue memorie sono state costruite prima che il banco fosse isolato; non c'è un set nuovo mai
     visto (LongMemEval_M fa le stesse 500 domande su storie più lunghe, quindi non sono domande nuove, e costruirne le
     memorie costa circa dieci volte tanto).

## Domande aperte

1. `EventsListMax` 100 righe ed `EventsListChars` 16.000: abbastanza per un anno di menzioni di una categoria, piccolo
   per un contesto. Proposto così.
2. Anche `memory_search` (i file) dovrebbe avere `every`? Proposta: non ora; gli errori sono in ciò che è stato detto.
