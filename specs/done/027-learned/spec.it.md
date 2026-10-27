# Cosa Filum impara della persona, separato da quello che gli viene detto

> Stato: Implementata (2026-10-04). Rivista (il proprietario ha delegato le spec della fase E, 2026-10-01). L'approccio è diverso dalla riga della roadmap per la 027 (bandit contestuali sulle proposte): segue la capacità 9 di `FEATURES.md`, e la scelta è chiesta al proprietario.
> Area: engine (memoria, istruzioni, consolidamento) / il turno / MCP
> Autore/data: federico rogora (redatta da Claude), 2026-10-04

## Problema

La memoria tiene quello che la persona dice: regole nel nucleo, fatti, collezioni. Quello che nota senza che le venga detto non ha una casa, quindi o si perde, o finisce nel nucleo come se la persona l'avesse detto:
- i nomi che dà a persone e cose;
- le sue abitudini;
- come preferisce le risposte.

La capacità 9 chiede un agente che lo impari in modo implicito, lo tenga visibile e modificabile, lo distingua da quello che gli è stato detto, non lo lasci mai scavalcare un'istruzione, e non impari mai da contenuti privati.

## Obiettivo

Quello che l'agente deduce sulla persona vive in un file tutto suo, letto a ogni messaggio dopo il nucleo, tenuto dal turno e dal consolidamento, e di proprietà della persona come qualunque altro file.

## Non obiettivi

- **Nessun modello nuovo e nessun addestramento:** la deduzione la fanno il modello del turno e il consolidamento, scritta come testo.
- **Nessuna modifica imparata al codice o agli strumenti dell'agente:** solo quello che sa della persona.

## Comportamento desiderato

1. **`/learned.md`.**
   - Un documento che la piattaforma crea con ogni memoria (e al primo bisogno in una più vecchia). Le sue righe sono deduzioni: una per riga, con il giorno in cui è stata notata e, dal consolidamento, gli eventi da cui viene.
   - Sta nella memoria come qualunque file: visibile, modificabile, con la sua cronologia.
   - Resta fuori dalla mappa generata, perché le sue righe sono già nelle istruzioni.
2. **Letto a ogni messaggio.**
   - Le istruzioni portano le sue righe dopo il nucleo, sotto "Cosa hai imparato sulla persona (dedotto, mai sopra il nucleo)".
   - Al massimo 3.000 caratteri; oltre, si portano le righe più recenti e si contano le altre.
   - Le dà anche `memory_overview`.
3. **Scritto dal vivo e dal consolidamento.**
   - Le istruzioni (ospitato e MCP) dicono all'agente cosa ci va: le parole per persone, luoghi e cose; abitudini e ritmi; come la persona preferisce le risposte.
   - L'agente aggiunge una riga con `memory_append` e toglie quella che non vale più con `memory_edit`.
   - Il consolidamento fa lo stesso a partire dagli eventi, citandoli. Può aggiungere al file anche se l'ultima modifica è della persona, ma in quel caso non può riscriverlo.
4. **Quello che viene detto vince sempre, il privato non insegna mai.**
   - Quello che la persona ha detto esplicitamente va nel nucleo.
   - Le istruzioni dicono che una deduzione non lo scavalca mai.
   - Al consolidamento non vengono mai dati eventi privati, né per questo né per altro.
5. **"Cosa hai imparato di me?"** ha risposta dal file, distinguendo quello che è stato detto (il nucleo) da quello che è stato dedotto.

## Verifica di piattaforma

1. **Generico:** sì. I tratti sono quelli che una persona mostra, in qualunque dominio.
2. **Sensibilità:** il consolidamento non legge mai eventi privati; il file è della persona, che può correggerlo o svuotarlo.

## Criteri di accettazione

1. Data una memoria nuova, allora `/learned.md` esiste, creato dalla piattaforma, senza deduzioni; non è nella mappa.
2. Data una riga nel file, quando la persona scrive, allora le istruzioni del turno la portano dopo il nucleo.
3. Date più righe di quante ne stanno, allora si portano le più recenti e si contano le altre.
4. Dato che l'ultima modifica al file è della persona, quando un passaggio ci aggiunge una riga, allora la riga viene aggiunta; quando un passaggio lo riscrive, allora viene rifiutato.
5. Dato un evento privato, allora nessun passaggio lo riceve.
