# Tendenze: previsioni e cose che spiccano, su qualunque collezione nel tempo

> Stato: Implementata (2026-10-04). Rivista (il proprietario ha delegato le spec della fase E, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (strumenti)
> Autore/data: federico rogora (redatta da Claude), 2026-10-04
> Roadmap: fase E, spec 026 ("una previsione con il suo errore su serie sintetiche; un'anomalia trovata e spiegata"). L'altra metà della 026 nella roadmap, il calcolo in sandbox (capacità 5), è lasciata fuori e chiesta al proprietario.

## Problema

Una persona che tiene una collezione con una data e un numero ha i totali esatti da `collection_aggregate`. Ma a una domanda come "dove sta andando?" o "cosa c'è stato di strano?" risponde l'aritmetica del modello, cioè una supposizione presentata come un risultato.

## Obiettivo

L'agente risponde dove sta andando un valore e cosa spicca a partire da un calcolo: una previsione con la fascia in cui probabilmente cade e l'errore che il metodo ha avuto sul passato della serie stessa, e i periodi lontani dalla tendenza, ognuno con quello che ci si aspettava. Sempre dichiarate come stime.

## Non obiettivi

- **Niente sandbox e niente codice scritto dall'agente** (capacità 5): chiesto al proprietario.
- **Nessun modello e nessuna dipendenza nuova:** codice semplice, deterministico. ML.NET era approvato ma qui non serve.
- **Niente stagionalità:** la previsione è un livello e una tendenza. Le serie stagionali più avanti, quando i dati di una persona lo chiederanno.

## Comportamento desiderato

1. **Un solo strumento, `collection_trend(path, dateColumn, column?, operation = sum, period = month, horizon = 3)`.**
   - La serie è la collezione raggruppata per giorno, settimana, mese o anno del campo data, esattamente come la raggruppa `collection_aggregate`.
   - La somma o il conteggio di un periodo senza righe vale zero; una media salta il periodo.
2. **La previsione.**
   - Smoothing lineare di Holt: i suoi due pesi sono scelti sugli errori a un passo della serie.
   - Ogni periodo previsto ha una stima e una fascia di circa il 95% (1,96 volte l'errore a un passo, che cresce con la distanza).
   - Servono almeno 6 periodi; con meno, lo strumento lo dice e non prevede niente.
3. **Il suo errore, sul proprio passato.** Gli ultimi periodi (un quarto della serie, almeno uno, al massimo l'orizzonte) vengono previsti da quelli prima. Si dà l'errore medio, accanto a quello che si avrebbe ripetendo l'ultimo valore.
4. **Cosa spicca.** Da 8 periodi in su:
   - l'errore a un passo di ogni periodo viene valutato con uno z-score robusto (mediana e deviazione assoluta mediana);
   - oltre 3,5 il periodo spicca, con il suo valore, quello che la tendenza si aspettava e quante deviazioni tipiche li separano.
5. **Le istruzioni:** dove sta andando un valore e cosa spicca vengono da `collection_trend`, sempre chiamati stima con la loro fascia.

## Verifica di piattaforma

1. **Generico:** sì. Qualunque collezione con una data e un numero, in qualunque dominio.
2. **Sensibilità:** legge una collezione come `collection_aggregate`, con le stesse regole, e non scrive niente.

## Criteri di accettazione

1. Data una crescita mensile costante, quando se ne calcola la tendenza, allora i periodi successivi vengono previsti lungo la retta, entro una fascia, e l'errore sul proprio passato è piccolo e minore di quello che si avrebbe ripetendo l'ultimo valore.
2. Data una serie con un periodo lontano dagli altri, allora quel periodo spicca, con quello che la tendenza si aspettava.
3. Data una serie con un mese senza righe, allora il mese vale zero; dati meno di 6 periodi, allora non si prevede niente e lo strumento dice perché.
4. Dato lo strumento in un turno, allora la sua risposta chiama stime i suoi numeri e nomina quello che spicca, e il suo passo dice cosa è stato calcolato.

## Domande aperte

Nessuna: il proprietario ha delegato le decisioni della fase E. La sandbox è chiesta nel file delle domande.
