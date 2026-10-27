# Assorbire: gli import da file

> Stato: Implementata (2026-10-04). Rivista (il proprietario ha delegato le spec della fase E, 2026-10-01: "finisci filum da solo con le info che ci siamo detti e solo alla fine mi spieghi come funziona")
> Area: engine (il registro degli eventi) / MCP / HTTP
> Autore/data: federico rogora (redatta da Claude), 2026-10-04
> Roadmap: fase E, spec 021 ("un export diventa memoria con la sua provenienza; un'anteprima mostra cosa verrebbe tenuto prima di scrivere qualunque cosa")

## Problema

Filum impara solo quello che la persona scrive. Ma una persona ha già anni della sua vita in file: export delle chat, caselle di posta, calendari, fogli di calcolo. Oggi niente di tutto questo può entrare nella memoria, se non incollandolo in una chat un messaggio alla volta.

## Obiettivo

Un file che la persona ha già diventa eventi nel suo registro, ognuno con la sua data e la sua fonte. Poi il consolidamento li trasforma in memoria, con la provenienza. Un'anteprima mostra cosa verrebbe importato prima di scrivere qualunque cosa.

## Non obiettivi

- **Niente connettori e niente OAuth:** solo file. Le fonti dal vivo sono la spec 024.
- **Nessun modello che tiene o scarta:** ogni elemento diventa un evento; decidere cosa conta tocca oggi al consolidamento e più avanti al filtro veloce (022).
- **Niente export della salute, PDF, audio o foto:** servono parser loro, o modelli. Più avanti.
- **Nessuna schermata nell'app:** l'endpoint c'è per l'app; la schermata è lavoro dell'app.

## Comportamento attuale

- Il registro degli eventi (spec 030) ha il tipo `imported` e la fonte `import`, ma niente li produce.
- Il consolidamento legge già gli eventi `imported` come quello che è stato detto.

## Comportamento desiderato

### 1. I formati

| Formato | Cos'è un elemento | La sua data | La sua identità |
|---|---|---|---|
| `whatsapp` (export `.txt`, Android e iOS) | un messaggio: chi e cosa, con le righe successive unite | quella del messaggio | nome della chat, ora, mittente e testo |
| `mbox` (posta) | una mail: mittente, oggetto, il testo semplice del corpo | il suo `Date` | il suo `Message-ID`, o il suo contenuto |
| `ics` (calendario) | un appuntamento: titolo, luogo, descrizione | il suo inizio | il suo `UID` e l'inizio |
| `csv` | una riga: ogni campo con il suo nome | la prima colonna i cui valori sono tutti date, altrimenti quella dell'import | il suo contenuto |

- Il formato si riconosce dal nome e dal contenuto del file, oppure si indica.
- L'ordine delle date di WhatsApp (prima il giorno o prima il mese) si ricava dal file.
- Le righe che non sono messaggi (avvisi di sistema) si saltano e si contano.
- Il corpo di una mail è la parte in testo semplice, oppure quella HTML senza tag. Quoted-printable e base64 vengono decodificati. Un corpo si taglia a 4.000 caratteri.

### 2. L'import

- Ogni elemento diventa un evento `imported`:
  - **fonte:** `import`;
  - **data:** quella dell'elemento, come `occurred_at`; il momento dell'import è `recorded_at`;
  - **testo:** l'elemento, con chi l'ha detto quando c'è qualcuno;
  - **sensibilità:** scelta per tutto l'import (normale di default).
- **Un file, una fonte:** tutti i suoi elementi condividono un id, il `conversationId` dell'import, con cui lo si ritrova e lo si dimentica.
- **Niente doppioni:** un elemento la cui identità è già nel registro si salta, e si conta.
- **Anteprima:** la stessa chiamata con `preview` vero non scrive niente. Restituisce il formato, quanti elementi sono nuovi, quanti doppioni, quante righe saltate, e i primi cinque elementi nuovi come verrebbero registrati.
- **Limiti:** al massimo 5.000 elementi e 20 MB per file. Oltre non si scrive niente, e alla persona si dice di dividere il file.
- **Dimenticare un import** è dimenticare la sua fonte: `POST …/memory/forget` con l'id dell'import (spec 030).

### 3. Dove si offre

- **`filum-mcp`:** lo strumento `memory_import(path, format?, sensitivity?, preview = true)` legge un file sulla macchina della persona.
  - Il modello dell'ospite fa prima l'anteprima, mostra alla persona cosa verrebbe importato, e importa con `preview` falso solo se lei è d'accordo.
  - Poi l'ospite riordina gli eventi con `memory_consolidate` (spec 030).
- **Ospitato:** `POST …/memory/import`, un upload multipart (`file`, `format`, `sensitivity`, `preview`). Restituisce quello che restituisce l'anteprima, più l'id dell'import. Il consolidamento prende gli eventi come tutti gli altri, 200 per passaggio, finché non ne restano in sospeso.

## Verifica di piattaforma

1. **Generico:** sì. I formati sono formati di file, non domini; il contenuto degli elementi è un dato della persona.
2. **Sensibilità e privacy:**
   - la sensibilità si sceglie all'ingresso e la porta ogni evento;
   - un import si dimentica tutto intero con il suo id;
   - i file si leggono dove sono, e l'import da solo non manda niente da nessuna parte;
   - i test usano solo file sintetici.

## Criteri di accettazione

1. Dato un export di WhatsApp (Android o iOS, prima il giorno o prima il mese, messaggi su più righe, avvisi di sistema), quando viene letto, allora ogni messaggio è un elemento con mittente, testo completo e ora, e gli avvisi si saltano e si contano.
2. Dato un mbox con una mail in testo semplice, una multipart e una solo HTML, quando viene letto, allora ogni mail è un elemento con data, mittente, oggetto e corpo leggibile.
3. Dato un calendario ICS con righe spezzate, caratteri con escape, appuntamenti di un giorno intero e con orario, quando viene letto, allora ogni appuntamento è un elemento con inizio, titolo, luogo e descrizione.
4. Dato un CSV con una colonna di date, quando viene letto, allora ogni riga è un elemento datato da quella colonna; senza, dall'ora dell'import.
5. Data un'anteprima, quando gira, allora non viene scritto niente, e tornano i conteggi e i primi elementi nuovi.
6. Dato un import, quando gira di nuovo sullo stesso file, allora non si aggiunge nessun evento e ogni elemento conta come doppione.
7. Dato un import con sensibilità privata, allora ogni evento è privato e `events_search` lo trova solo con includePrivate.
8. Dato un import, quando il suo id viene dimenticato, allora tutti i suoi eventi spariscono.
9. Dato un file oltre i limiti, allora non viene scritto niente e il rifiuto dice di dividerlo.
10. Dato `filum-mcp`, quando `memory_import` fa l'anteprima e poi importa un file, allora gli eventi sono in `.filum/events.jsonl` e `memory_consolidate` li passa all'ospite.
11. Dato il servizio ospitato, quando un file viene caricato su `memory/import`, allora gli eventi vengono registrati per la persona che l'ha caricato, e un'altra persona non ne vede nessuno.

## Domande aperte

Nessuna: il proprietario ha delegato le decisioni della fase E.
