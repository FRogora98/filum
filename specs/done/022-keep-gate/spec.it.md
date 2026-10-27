# Il filtro veloce: tenere o scartare, in un millisecondo

> Stato: Implementata (2026-10-04). Rivista (il proprietario, 2026-10-04: "parti dallo step direttamente successivo", dopo la spec 021; fase E delegata). Prima fetta della 022 della roadmap: tenere o scartare. La sensibilità, il file di destinazione, le contraddizioni e le skill sono fette successive.
> Area: engine (consolidamento) / `Filum.Embeddings` / eval
> Autore/data: federico rogora (redatta da Claude), 2026-10-04

## Problema

Un passaggio di consolidamento passa a un modello tutti gli eventi dall'ultimo passaggio. La maggior parte di quello che le persone dicono a un assistente non riguarda la loro vita: una ricetta, un copione, una traduzione, una domanda su uno strumento. Su LongMemEval `S` circa metà delle sessioni di una storia sono chiacchierate generiche di questo tipo. Il modello le legge tutte, a prezzo pieno, senza trovarci niente.

## Obiettivo

Un piccolo classificatore sul vettore dell'evento (spec 023) dice in circa un millisecondo se un evento racconta qualcosa della vita della persona. Al passaggio si danno solo quelli. Degli eventi che contengono una risposta ne tiene il 98% o più, e un passaggio che non avrebbe niente da leggere non chiama nessun modello.

## Non obiettivi

- **Il turno non viene filtrato:** a quello che la persona dice si risponde sempre, e resta sempre nel registro.
- **Non si cancella niente:** un evento scartato resta nel registro e si può cercare. Solo, non viene dato al passaggio.
- **Nessun modello grande e nessuna GPU:** una regressione logistica sui 384 numeri del vettore che la ricerca fa già.

## Comportamento desiderato

1. **Addestramento, senza costi.**
   - `longmemeval --gate --embeddings <cartella>` del runner prende i messaggi della persona da LongMemEval `S`. Quelli delle sessioni personali valgono "tenere", quelli delle sessioni generiche (ShareGPT, UltraChat) valgono "scartare".
   - Si addestra sulle domande di una parte del file e si prova sulle altre.
   - Si sceglie la soglia che tiene almeno il 98% dei messaggi di prova che contengono una risposta.
   - Scrive pesi e soglia, e un report: accuratezza, prove tenute, quota scartata.
   - I pesi viaggiano con il modello, in `Filum.Embeddings`.
2. **Il filtro in un passaggio.**
   - Con un modello e un filtro, gli eventi che un passaggio leggerebbe ricevono un punteggio, e al modello vanno solo quelli sopra la soglia.
   - Il passaggio li chiude tutti come letti.
   - Se non resta niente, il passaggio si chiude senza chiamare il modello.
   - Lo stesso in `memory_consolidate` di `filum-mcp`.
3. **Spento senza il modello:** un host o `filum-mcp` senza il modello di ricerca consolida come oggi.

## Verifica di piattaforma

1. **Generico:** sì. Il classificatore distingue "riguarda la vita della persona" da "un compito generico", in qualunque dominio e lingua che il modello legge.
2. **Sensibilità:** invariata. Gli eventi privati non arrivano a un passaggio già in partenza (spec 027).

## Criteri di accettazione

1. Date le domande di prova di `S`, quando il filtro valuta i messaggi della persona, allora tiene almeno il 98% di quelli che contengono una risposta, e accuratezza e quota scartata sono nel report.
2. Dato un passaggio con un filtro, quando gli eventi in sospeso sono una chiacchierata generica e un messaggio sulla persona, allora al modello va solo il secondo, e vengono chiusi entrambi.
3. Dato un passaggio con un filtro e solo eventi generici in sospeso, allora non si chiama nessun modello e il passaggio li chiude.
4. Senza modello, il passaggio è come oggi.

## Risultato (2026-10-04)

`evals/reports/gate-20261004-171303/`: 500 domande di `S`, le prime tre quinte per addestrare e le altre per provare; vettori di `multilingual-e5-small`; regressione logistica; costo zero.

| Misura (domande di prova) | Valore |
|---|---|
| Messaggi che contengono una risposta, tenuti | **99,1%** di 422 |
| Messaggi personali tenuti | 97,5% |
| Messaggi generici scartati | **71,5%** |
| Accuratezza | 84,5% |
| Soglia | 0,349 |
