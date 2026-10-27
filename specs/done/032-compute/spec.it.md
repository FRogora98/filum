# Calcolo: il codice dell'agente, in un interprete isolato

> Stato: Implementata (2026-10-04). Rivista (il proprietario, 2026-10-04: "ambiente isolato va bene, é una feature che dobbiamo per forsza fare"; fase E delegata). L'interprete (Jint) è una dipendenza nuova, chiesta al proprietario nel file delle domande.
> Area: engine (strumenti)
> Autore/data: federico rogora (redatta da Claude), 2026-10-04
> Capacità 5 di `FEATURES.md`; la metà della 026 della roadmap che la spec 026 aveva lasciato fuori.

## Problema

`collection_aggregate` e `collection_trend` rispondono a tipi fissi di domande. Tutto il resto torna all'aritmetica del modello, cioè una supposizione presentata come un risultato:
- una simulazione ("se risparmio il 15% in più al mese, quando arrivo all'obiettivo?");
- un confronto fra due collezioni;
- un "e se…", o un raggruppamento su misura.

## Obiettivo

L'agente scrive un breve programma per la domanda e lo esegue in un interprete isolato, con accesso in sola lettura alle collezioni che nomina. I numeri della risposta vengono dall'esecuzione, e il codice resta lì da controllare.

## Non obiettivi

- **Niente container, niente processi per persona, niente rete, niente file:** un interprete dentro il processo del motore, che non raggiunge niente oltre ai dati che gli vengono dati.
- **Nessuna scrittura dal codice:** un'esecuzione restituisce valori. Le modifiche alla memoria passano dagli strumenti e dai loro controlli.
- **Ancora niente programmi salvati:** trasformare un'esecuzione in una procedura o in un artefatto (capacità 10) è per dopo.

## Comportamento desiderato

1. **Lo strumento:** `compute_run(code, collections?, includePrivate?)`.
   - `code` è il corpo di una funzione JavaScript, eseguita in modalità strict. Restituisce un valore, e `print(…)` aggiunge righe all'output.
   - `collections` elenca percorsi di collezioni. Ognuna arriva al codice come `data["/percorso.csv"]`, un array di righe, un oggetto per riga, campo → valore. Un valore che si legge come numero è un numero; tutto il resto è testo.
   - Le collezioni private arrivano solo con `includePrivate`, come per le ricerche.
2. **Isolamento e limiti.**
   - L'interprete non ha accesso a .NET, alla rete, al disco né all'orologio oltre `Date`.
   - Si ferma a 2 secondi, 64 MB, un milione di istruzioni e 100 livelli di ricorsione.
   - Riceve al massimo 20.000 righe.
   - Il risultato è al massimo 8 KB in JSON, e l'output al massimo 4 KB.
   - Oltre un limite l'esecuzione fallisce con il motivo, e non succede nient'altro.
3. **Cosa vedono l'agente e la persona.**
   - Il modello riceve il risultato in JSON e l'output.
   - Il passo del turno dice quali collezioni e quante righe sono state usate, e porta il codice (nei suoi `data`), così l'app può mostrare cosa è stato eseguito.
   - Un'esecuzione che fallisce dà al modello l'errore, senza stack trace.
4. **Le istruzioni:** una domanda che richiede un'elaborazione oltre un totale o una tendenza riceve un programma eseguito con `compute_run`. I suoi numeri vengono dall'esecuzione, e la risposta dichiara le ipotesi.

## Verifica di piattaforma

1. **Generico:** sì. Codice sulle collezioni della persona, in qualunque dominio.
2. **Sensibilità e isolamento:**
   - le collezioni private arrivano solo se richieste;
   - il codice non raggiunge niente oltre alle righe che riceve, e non scrive niente.

## Criteri di accettazione

1. Date due collezioni, quando il codice le unisce e restituisce un numero, allora il numero è quello giusto, e il passo nomina entrambe le collezioni e le righe usate.
2. Dato codice che gira all'infinito, alloca senza fine o va troppo in profondità con la ricorsione, allora l'esecuzione si ferma al suo limite con il motivo, più o meno entro il limite di tempo.
3. Dato codice che cerca di raggiungere .NET, `require`, `fetch` o `import`, allora fallisce: niente di questo esiste.
4. Data una collezione privata, quando viene nominata senza `includePrivate`, allora l'esecuzione viene rifiutata; con, viene eseguita.
5. Dato un risultato oltre gli 8 KB, allora l'esecuzione fallisce e dice di restituire meno.
