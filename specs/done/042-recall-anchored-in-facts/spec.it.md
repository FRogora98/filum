# Richiamo ancorato ai fatti

> Stato: Implementata (2026-10-18). Rivista (il titolare, 2026-10-18, idea 3 del documento sullo stato dell'arte; il
> reranker approvato solo spento di default e scaricato su richiesta)
> Area: motore (`events_search`: `EventSearch`, `EventVectorService`, `EventTools`, `FactLinks`), embedding, filum-mcp
> Autore/data: federico rogora (scritta da Claude), 2026-10-18

## Problema

Su LongMemEval (400 domande, due turni) la memoria a fatti di Filum è
risultata alla pari con un BM25 che dà al modello le cinque sessioni migliori intere. Una lettura dei suoi errori dice perché:
- **Cosa consegna una ricerca.** `events_search` restituisce al massimo 8 turni (`MemoryOptions.EventsSearchMax`),
  ciascuno tagliato a un estratto di circa 1.000 caratteri (`EventSearch.Excerpt`), con la risposta dopo tagliata a 300
  (`EventTools.SearchEvents`). La risposta spesso sta qualche turno più in là, o in fondo a una risposta lunga.
- **I fatti si raggiungono solo per nome.** Un fatto compare accanto a una ricerca solo se ogni parola del nome del suo
  soggetto è nella domanda (`FactLinks.About`).
- **Un fatto non riporta a dove è stato detto.** Un fatto tiene gli id dei suoi eventi d'origine, ma la ricerca non
  mostra la loro conversazione; `events_read` (spec 038) è stato chiamato in 13 risposte su 200.
- **Il numero di risultati è fisso.** Otto turni, che la domanda tocchi una conversazione o sei.

## Obiettivo

Una ricerca di ciò che è stato detto restituisce i fatti che corrispondono alla domanda, per parole e per significato,
ciascuno con la conversazione da cui viene, e le conversazioni migliori, scelte da dove cala il punteggio, intere quanto
un budget di caratteri permette; un reranker locale può affinare l'ordine, spento finché la persona non lo accende.

## Fuori scopo

- Nessun cambio a `IMemoryService.SearchEventsAsync` né a `MemoryEventHit`: l'ordinamento dei turni resta l'API del
  motore; lo strumento raggruppa e taglia ciò che mostra.
- Nessun vettore dei fatti in un archivio e nessuna tabella nuova: il vettore di un fatto è derivato, fatto quando serve
  e tenuto nel processo.
- Nessuna espansione temporale della domanda (una spec successiva), nessuno strumento nuovo, nessun cambio a nomi o
  parametri del catalogo.
- Il reranker non porta file nel pacchetto: si scarica solo quando la persona lo accende.

## Comportamento attuale

- Ordinamento: BM25 e, con il modello di embedding, coseno sui vettori e5, fusi per rango reciproco (`EventSearch.Fuse`,
  profondità 50), i primi `EventsSearchMax` turni (`EventVectorService.RankAsync`; solo parole in `NullEventVectors`).
- Testo: ogni turno con data, numero, titolo della conversazione e un estratto, poi 300 caratteri della risposta; prima
  i fatti, quando la domanda nomina il loro soggetto.
- Contenuto strutturato: i turni trovati (`ToolData.Matches`, spec 040).

## Comportamento desiderato

1. **Fatti che corrispondono.** Prima delle conversazioni, `events_search` mostra fino a 5 fatti validi ora che
   corrispondono alla domanda: quelli il cui soggetto (o un alias, spec 041) è nominato, come oggi, poi gli altri
   ordinati per BM25 su "soggetto attributo valore" fuso con il coseno quando il modello è caricato. Ogni riga dice da
   dove viene (`from event #N`) quando ha fonti.
2. **Le loro conversazioni.** La conversazione della prima fonte dei uno o due fatti migliori viene per prima fra le
   conversazioni mostrate, segnata come fonte di quel fatto.
3. **Conversazioni, non turni.** I turni trovati si raggruppano per conversazione (un evento fuori da una conversazione
   con gli altri del suo giorno, come in `events_read`); una conversazione vale quanto il suo turno migliore.
4. **Numero adattivo.** Si mostrano le conversazioni migliori fino al calo di punteggio più grande fra le prime
   `RecallSessionsMax` (default 5), almeno una; la conversazione di un fatto del punto 2 si mostra sempre.
5. **Un budget.** Ciò che la ricerca mostra è al massimo `RecallBudgetChars` caratteri (default 24.000), ripartiti fra le
   conversazioni nel loro ordine: una che sta nella sua quota si mostra intera, in ordine; una più lunga mostra i suoi
   turni trovati interi (o, quando un turno da solo supera la quota, un estratto attorno alla domanda), ciascuno con il
   turno prima e la risposta dopo quando ci stanno, e dice quale evento leggere per il resto con `events_read`. Ciò che
   una conversazione lascia della sua quota passa alla successiva.
6. **Un reranker, spento di default.** Un host può dare al motore un `IReranker` (`UseReranker`): i migliori
   `RerankDepth` turni (default 20) della fusione vengono riordinati dai suoi punteggi prima del raggruppamento. Senza,
   l'ordine è quello della fusione. Un reranker che fallisce non si usa per quella ricerca, e la ricerca risponde lo
   stesso.
7. **Il reranker locale.** `Filum.Embeddings` dà `BgeReranker` (bge-reranker-v2-m3, Apache-2.0, l'export ONNX int8,
   circa 570 MB, su CPU, input tagliati a 256 token) e `RerankerFiles` (da dove vengono i file, con i loro SHA-256: un
   download si usa solo se corrisponde). Il suo tokenizer è il file del modello e5.
8. **filum-mcp.** `FILUM_RERANKER=on` scarica il reranker nella cartella dei modelli in background e lo usa appena
   caricato; senza, o con altro valore, non si scarica nulla e nulla cambia. La ricerca funziona per parole e significato
   mentre si carica, e dopo un download fallito.
9. **Il contenuto strutturato** resta i turni trovati (spec 040), nell'ordine mostrato.

## Verifica sulla piattaforma

1. Generico: parole, significato e conversazioni di qualunque dominio; nessun modello chiamato, nessun dominio nominato.
2. Sensibilità: gli eventi privati si mostrano solo con `includePrivate`, come oggi, e la conversazione di un fatto si
   legge con la stessa regola; il reranker gira sulla macchina della persona e non manda nulla altrove.

## Criteri di accettazione

1. Dato `Anna · city: Lyon` registrato dall'evento N, quando la domanda è `which city did she move to` (non nomina Anna),
   allora il fatto compare, con `from event #N`.
2. Dato un fatto la cui prima fonte è nella conversazione C, quando una ricerca trova il fatto, allora i messaggi di C
   vengono per primi, segnati come fonte del fatto, anche se i turni di C non corrispondono alla domanda.
3. Date tre conversazioni il cui turno migliore sta molto sopra il resto, allora si mostrano quelle tre e nessun'altra
   (il calo), e mai più di `RecallSessionsMax`.
4. Data una conversazione più corta della sua quota, allora si mostrano tutti i suoi messaggi in ordine; data una più
   lunga, i suoi turni trovati interi con i vicini e un rimando a `events_read`.
5. Data qualunque ricerca, il testo mostrato è al massimo `RecallBudgetChars` più le righe che nominano date ed eventi.
6. Dato un reranker finto che inverte l'ordine, i turni vengono nel suo ordine; dato uno che lancia un'eccezione, la
   ricerca risponde nell'ordine della fusione.
7. Senza reranker e senza modello di embedding, una ricerca risponde per parole, come prima, con i punti 1-5.
8. `RerankerFiles` nomina ogni file con un URL a revisione fissa e uno SHA-256, e un file il cui hash non corrisponde non
   si tiene (un test con un server finto locale, come per i file del modello e5).
9. Dato filum-mcp senza `FILUM_RERANKER`, nessun reranker è registrato e nulla si scarica.
10. Lo snapshot del catalogo degli strumenti non cambia.

## Misura (prima che conti)

Un turno pre-registrato nelle note di valutazione, scritto prima di ogni run: domande di LongMemEval_S non
usate in quei due turni, il braccio dei fatti con questa spec contro il braccio dei fatti al motore appena prima di questa spec e contro BM25
top-5 sessioni, lo stesso modello che risponde (Haiku, sulla quota dell'abbonamento, una domanda alla volta con
`--stop-on-limit`), soglie e test (McNemar esatto) fissati prima. Il reranker si misura come braccio a sé solo dopo il
turno senza.

## Domande aperte

Nessuna: il titolare ha fissato l'ambito (richiamo ancorato ai fatti, budget adattivo, la conversazione d'origine nel
budget, il reranker spento di default e scaricato su richiesta) e le scelte tecniche sono del motore.
