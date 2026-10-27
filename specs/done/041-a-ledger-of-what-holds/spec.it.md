# Un registro di ciò che vale

> Stato: Implementata (2026-10-18). Rivista (il titolare, 2026-10-18, idea 1 del documento sullo stato dell'arte, versione prodotto)
> Area: motore (fatti: `FactService`, `FactTable`, strumenti dei fatti, istruzioni)
> Autore/data: federico rogora (scritta da Claude), 2026-10-18

## Problema

La tabella dei fatti (`/facts.csv`, spec 037) tiene ogni valore che un fatto ha avuto, con i suoi giorni. L'audit e lo
studio di design (uno studio dello stato dell'arte, nelle note di sviluppo) hanno trovato cosa non sa dire:
- **Una correzione sembra un cambiamento.** "Sam vive a Lione, non a Torino, mi ero sbagliato" chiude Torino in un
  giorno come se Sam si fosse trasferito; la memoria poi sostiene che Torino era vero fino a quel giorno.
- **Un fatto non può finire senza un nuovo valore.** "Ho venduto l'auto" non ha un valore successivo da registrare.
- **Un valore precedente viene rifiutato.** Un valore con un giorno prima di quello attuale è rifiutato, quindi una
  storia raccontata fuori ordine non si può completare.
- **Ripeterlo è un errore.** Registrare il valore che un fatto ha già viene rifiutato, e un modello lo legge come errore.
- **Chi l'ha scritto, quando e da quali parole si perde.** Una riga non dice quando è stata registrata, se dalla persona
  o dall'agente, né le parole da cui viene.
- **Nomi.** "Sam" e "Sam Lee" sono due soggetti.
- **Non si può chiedere il passato.** Non c'è modo di chiedere cosa valeva in un dato giorno.

## Obiettivo

I fatti sono un registro: ogni valore dice quando valeva, quando è stato registrato, da chi e da quali parole, e come è
finito (cambiato, corretto, ritirato, terminato); si può chiedere uno stato passato; e niente è legato a un dominio.

## Fuori scopo

- Nessun archivio a grafo e nessuna dipendenza nuova: `/facts.csv` resta l'unico posto dei fatti, un file che la persona
  può aprire. Un indice (SQLite, approvato) è rimandato a quando le dimensioni lo richiederanno.
- Una modifica a mano di `/facts.csv` resta una modifica del file (spec 012); non viene riletta come correzioni tipizzate.
- Nessuna ricerca automatica delle contraddizioni: è compito del consolidamento (una spec successiva).

## Comportamento desiderato

1. **Quattro colonne in più**, dopo le sette della spec 037, così il file resta leggibile dalle versioni precedenti (che
   le ignorano): `recorded_at` (UTC, `yyyy-MM-ddTHH:mm:ssZ`), `ended` (vuota finché è aperto; `changed`, `corrected`,
   `retracted` o `ended`), `author` (`agent` o `person`, vuota se non si sa) e `quote` (fino a 200 caratteri delle
   parole del primo evento di origine). Una tabella con le sole sette colonne si legge come prima e ottiene le quattro
   alla scrittura successiva; le sue righe vecchie le hanno vuote.
2. **`fact_record` ottiene `correction`** (vero quando la persona dice che il valore attuale era sbagliato). Il valore
   aperto si chiude allora come `corrected`: il suo `valid_to` diventa il suo stesso `valid_from`, quindi non è mai valso;
   il nuovo valore prende il suo tempo. Con `validFrom`, si corregge il valore che valeva in quel giorno, aperto o no
   (modificato dopo l'audit indipendente). Senza `correction`, il valore aperto si chiude come `changed` nel giorno del
   nuovo valore, come prima.
3. **`fact_end`, nuovo strumento:** chiude il valore aperto di un soggetto e attributo senza uno nuovo, in un giorno
   (`on`, oggi se vuoto) come `ended`; con `retract`, come `retracted`, cioè non è mai valso (`valid_to` = `valid_from`).
4. **Un valore precedente si colloca nel tempo.** Un valore con un giorno prima di quello aperto viene inserito al suo
   posto: vale dal suo giorno fino all'inizio del valore registrato successivo, e il valore che valeva in quel giorno si
   chiude lì come `changed`. Un valore registrato per un giorno che ha già un valore prende il suo posto, e vince il più
   recente detto, quanti che siano i valori di quel giorno; quello sostituito si chiude quel giorno come `changed`. Un
   valore collocato prima di uno chiuso da `fact_end` si chiude dove quello comincia; uno collocato dentro un periodo
   chiuso finisce quando quel periodo finiva; quando il valore successivo è lo stesso, comincia prima invece di essere
   aggiunto due volte. Nulla si rifiuta per il suo giorno
   (modificato il 2026-10-18, due volte: un valore può cambiare più volte in un giorno, e i fatti dati in ordine senza un
   giorno proprio sono tutti di oggi).
5. **Ripeterlo riesce.** Registrare il valore che un fatto ha già non scrive nulla e risponde che è già registrato; così
   anche un valore che già valeva nel giorno dato.
6. **Gli alias sono fatti.** Un fatto con l'attributo `alias` (`Sam Lee · alias: Sam`) rende il valore un altro nome del
   soggetto: chiedere i fatti di `Sam` dà quelli di `Sam Lee`, in `facts_current`, `facts_history` e nei fatti che le
   ricerche mostrano (`FactLinks`). Un soggetto può avere più alias: uno nuovo non chiude gli altri.
7. **Si può chiedere il passato.** `facts_current` ottiene `on` (un giorno): i valori che valevano quel giorno, mai quelli
   corretti o ritirati.
8. **I valori corretti e ritirati** non rispondono mai a cosa vale ora o valeva in un giorno; `facts_history` li mostra
   con il modo in cui sono finiti.
9. **Righe e output strutturato** mostrano come è finito un valore e chi l'ha registrato (`- Sam · city: Turin
   (2026-01-01, corrected 2026-03-01 by the person)`); il contenuto strutturato (spec 040) aggiunge `ended`,
   `recordedAt`, `author` e `quote`.
10. **La politica sui fatti** (`InstructionParts.FactPolicy`, spec 040) aggiunge: un valore che la persona dice sbagliato
    si registra con `correction`; qualcosa che non vale più, senza nulla al suo posto, si chiude con `fact_end`.
11. **Catalogo.** `fact_end` entra nel profilo core (14 strumenti). Il budget di dimensione del catalogo della spec 031
    (11.500 caratteri per tutti gli strumenti insieme) diventa un budget per profilo: il core al massimo 7.000
    caratteri, tutti gli strumenti al massimo 12.500. Un host che offre tutti gli strumenti l'ha scelto.

## Verifica di piattaforma

1. Generico: soggetti, attributi e valori di qualsiasi dominio; `alias` è l'unico attributo riservato.
2. Sensibilità: invariata; una citazione è al massimo ciò che l'evento di origine già contiene, e un evento privato non
   dà citazione: le sue parole non arrivano mai in `/facts.csv`.

## Criteri di accettazione

Gli stessi 10 di `spec.md`, nello stesso ordine.
