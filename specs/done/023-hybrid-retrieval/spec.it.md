# Ricerca ibrida: quello che è stato detto, trovato anche per significato

> Stato: Implementata (2026-10-04). Rivista (il proprietario ha delegato le spec della fase E, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (ricerca) / MCP / hosting / eval
> Autore/data: federico rogora (redatta da Claude), 2026-10-04
> Roadmap: fase E, spec 023 ("il recall@k sul benchmark sale rispetto alla ricerca testuale"). Dipendenze nuove approvate dal proprietario il 2026-10-01: ONNX Runtime, i modelli di embedding, ognuno facoltativo.

## Problema

`events_search` ordina per parole (BM25). È misurata, senza costi, su tutte le 470 domande di LongMemEval `S` che non chiedono di astenersi, circa 500 eventi ciascuna, con la sola ricerca:

- una sessione che contiene la risposta è fra i primi 5 risultati per il 93,8% delle domande;
- ma di **tutte** le sessioni che contengono la risposta, solo l'86,7% è fra i primi 10;
- il buco è nelle domande che richiedono più sessioni: più sessioni 82,0%, tempo 81,1%.

Una ricerca per parole perde quello che è stato detto con altre parole ("ho comprato una felce" per "piante"), e le lingue diverse da quella della domanda.

## Obiettivo

`events_search` trova anche per significato, con un piccolo modello locale, così salgono più sessioni della risposta. Senza il modello funziona come oggi.

## Non obiettivi

- **Niente GPU, niente embedding a pagamento, niente database vettoriale:** un piccolo modello multilingue sulla CPU, vettori accanto agli eventi.
- **Nessun cambio alla superficie degli strumenti:** `events_search` tiene nome e parametri; cambia il suo ordinamento.
- **I file non vengono trasformati in vettori:** solo il registro degli eventi. `memory_search` resta una ricerca testuale sui file.

## Comportamento desiderato

1. **Il modello.**
   - `multilingual-e5-small`: licenza MIT, 384 dimensioni, quantizzato a circa 118 MB, molte lingue.
   - Gira tramite ONNX Runtime con il suo tokenizzatore SentencePiece, in una libreria separata, `Filum.Embeddings`, così il motore non prende nessuna dipendenza nuova.
2. **I vettori.**
   - Un evento detto, risposto o importato riceve un vettore, tenuto accanto agli eventi: `.filum/vectors.jsonl` in una cartella, la tabella `memory_event_vectors` in Postgres.
   - I vettori si fanno in background: da un passaggio di consolidamento in un host, dopo un import o un riordino in `filum-mcp`. Una ricerca ne fa al massimo 32 mancanti (`Memory:VectorsPerSearch`), così resta veloce: un vettore costa circa 25 ms a messaggio sulla CPU di un portatile.
   - I vettori sono dati derivati. Dimenticare un evento dimentica il suo vettore, e un vettore perso si rifà.
3. **L'ordinamento.**
   - Con un modello, `events_search` fonde due ordinamenti per rango reciproco (k = 60): quello delle parole (BM25) e quello del significato (coseno fra il vettore della domanda e quelli degli eventi).
   - La finestra di date e gli eventi privati seguono le stesse regole di oggi.
4. **Da dove viene il modello.**
   - **`filum-mcp`:** in `~/.filum/models/multilingual-e5-small` (o `FILUM_MODELS`), scaricato in background al primo avvio da Hugging Face, controllato con il suo SHA-256. La ricerca lo usa appena c'è. `FILUM_EMBEDDINGS=off` lo spegne.
   - **Un host:** registra il modello come `IEmbedder` del motore (`services.AddSingleton<IEmbedder>(new E5Embedder(cartella))`, da `Filum.Embeddings`), così un host che non lo vuole non prende la dipendenza da ONNX. Senza, un host cerca per parole come oggi.
5. **Misurata, senza costi.** `longmemeval --retrieval --embeddings <cartella>` del runner gioca la sola ricerca, con il modello, sulle stesse domande.

## Verifica di piattaforma

1. **Generico:** sì. Il modello legge qualunque testo in qualunque lingua.
2. **Sensibilità e privacy:**
   - il modello gira sulla macchina che tiene la memoria, e non viene mandato niente da nessuna parte;
   - gli eventi privati si trovano solo quando li si chiede, come oggi;
   - l'unica chiamata di rete è lo scaricamento del modello, che non porta dati personali.

## Criteri di accettazione

1. Dato il modello, quando un testo viene trasformato in vettore, allora i suoi token sono quelli del riferimento calcolato con il tokenizzatore del modello (`tokenizers`) e ONNX Runtime in Python, e il coseno fra il suo vettore e quello del riferimento è sopra 0,999.
2. Data una memoria con un modello, quando si cerca quello che è stato detto con parole diverse da quelle usate ("piante" per "ho comprato una felce"), allora lo si trova; senza modello, la stessa ricerca trova quello che trova oggi BM25.
3. Dato un evento dimenticato, allora anche il suo vettore sparisce, su ogni store.
4. Dato `filum-mcp` senza modello sul disco, quando parte, allora risponde subito e cerca per parole, e il modello viene scaricato in background e usato appena c'è; `FILUM_EMBEDDINGS=off` non scarica niente.
5. Dato LongMemEval `S` (le 470 domande), quando si misura la sola ricerca con il modello, allora la quota delle sessioni della risposta trovate nei primi 10 è più alta dell'86,7%, e il recall@5 non è sotto il 93,8%.

## Domande aperte

Nessuna: il proprietario ha delegato le decisioni della fase E. Il modello e il modo in cui l'host lo distribuisce sono chiesti nel file delle domande, e possono ancora cambiare.

## Risultato (2026-10-04)

La sola ricerca sulle 470 domande di LongMemEval `S` che non chiedono di astenersi, senza costi (`evals/reports/retrieval-20261004-083945-words/` e `…-110056-hybrid/`):

| | Parole (BM25) | Parole e significato |
|---|---|---|
| Recall@5, una sessione con la risposta | 93,8% | **96,6%** |
| Le sessioni della risposta trovate nei primi 10 | 86,7% | **91,9%** |
| … più sessioni | 82,0% | **89,9%** |
| … tempo | 81,1% | **87,5%** |
| … estrazione | 96,0% | 98,0% |
| … aggiornamento | 98,6% | 98,6% |

- Fare i vettori di una storia di 500 messaggi richiede circa 13 secondi sulla CPU di un portatile (circa 25 ms a messaggio).
- `filum-mcp` pubblicato (file singolo, win-x64) scarica il modello al primo avvio e trova "I bought a fern" per "plants".
