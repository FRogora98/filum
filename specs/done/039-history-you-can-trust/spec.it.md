# Una storia di cui fidarsi

> Stato: Implementata (2026-10-18). Rivista (il titolare, 2026-10-18, gap 4 dell'audit del motore, "integrità della storia")
> Area: motore (store a cartella: revisioni, scritture, vettori)
> Autore/data: federico rogora (scritta da Claude), 2026-10-18

## Problema

Lo store a cartella tiene ogni revisione di ogni file in `.filum/revisions.jsonl`, dalla 1.2 per lo più come modifica
rispetto alla revisione precedente (il formato a delta della spec 037). Un audit indipendente ha trovato quattro modi in
cui la storia può essere sbagliata senza che nessuno lo sappia:
- **Una riga danneggiata spezza una catena in silenzio.** Una riga che non si legge viene saltata (`JsonLines.Parse`), e
  una modifica senza la sua base viene ricostruita da un testo vuoto (`RevisionLog.ContentOf`): tutte le revisioni
  successive di quel file, fino a 256, tornano sbagliate e niente lo dice.
- **Un crash può trasformare la modifica dell'agente in quella della persona.** Una scrittura mette prima il file su
  disco e poi aggiunge la sua revisione (`FileChangeSaver.Save`); un crash tra le due lascia un file che il log non
  conosce, e la chiamata successiva lo adotta come modifica a mano della persona (`FolderScanner`).
- **Una rinomina può perdere dati in un crash.** Un file viene scritto in un file temporaneo e rinominato
  (`AtomicFile`), ma il temporaneo non viene scaricato su disco prima della rinomina.
- **Si mescolano vettori di un altro modello.** `.filum/vectors.jsonl` non dice quale modello di embedding li ha fatti;
  dopo un cambio di modello, vettori vecchi e nuovi vengono confrontati come se fossero lo stesso spazio.

## Obiettivo

La storia è giusta o sbagliata in modo esplicito, un crash non cambia mai chi ha scritto cosa, e i vettori si
confrontano solo con vettori dello stesso modello.

## Fuori scopo

- Nessun cambiamento al contratto pubblico `IMemoryStore` oltre alle aggiunte sotto; lo store Postgres degli host non
  cambia.
- Nessuna firma o cifratura della storia: un hash rileva un danno, non ferma chi riscrive il log.
- Nessuno strumento di riparazione: il danno viene segnalato con il punto in cui inizia; la persona tiene i suoi file.

## Comportamento desiderato

1. **Formato 3.** `.filum/format` diventa 3. Una cartella in formato 1 o 2 passa al 3 quando viene aperta (cambia solo il
   file di formato); le sue righe vecchie si leggono come prima. Un motore che conosce solo il formato 2 rifiuta una
   cartella in formato 3 (spec 037).
2. **Ogni nuova riga di revisione porta l'hash del suo contenuto** (`sha`: SHA-256 del contenuto UTF-8, esadecimale
   minuscolo), sia le righe intere sia quelle di modifica. Le righe scritte prima del formato 3 non lo hanno.
3. **Controllo in lettura.** Quando il contenuto di una revisione viene ricostruito o letto, si confronta con il suo
   hash. Una modifica senza la sua base, o un contenuto che non corrisponde al suo hash, ferma la lettura con un errore
   che nomina il percorso del file, la revisione e la prima revisione non ricostruibile, e dice che i file su disco non
   sono toccati. Una riga senza hash si legge come prima, salvo che una base mancante ora è un errore anche per lei.
4. **Ordine di scrittura e recupero.** Una modifica aggiunge prima la sua revisione, poi scrive il file, poi salva lo
   stato. Quando una cartella viene aperta:
   - le revisioni più recenti dello stato salvato vengono riapplicate ai file (la modifica viene completata, come
     l'aveva fatta l'agente);
   - senza uno stato salvato, un file il cui contenuto è quello della revisione precedente all'ultima viene completato
     allo stesso modo;
   - solo ciò che poi differisce ancora dall'ultima revisione è una modifica a mano della persona.
5. **Scritture durevoli.** Un file temporaneo viene scaricato su disco prima di essere rinominato sopra quello vero.
6. **I vettori conoscono il loro modello.** `IEmbedder` ottiene `ModelId` (un default null lascia compilare gli embedder
   esistenti). Uno store può implementare la nuova `IMemoryVectorModels`: l'id del modello con cui sono stati fatti i
   suoi vettori, e un modo per eliminarli tutti. Quando l'id dell'embedder differisce da quello dello store, i vettori
   vengono eliminati e rifatti; uno store senza l'interfaccia, o un embedder senza id, si comporta come prima. Lo store a
   cartella tiene l'id in `.filum/vectors.model`; una cartella senza quel file ha vettori dell'unico modello usato prima,
   che si tengono e si marcano con l'id attuale.
7. **Test di proprietà e di crash.** Il diff della spec 037 viene provato con testi casuali (applicare le modifiche tra
   due testi dà il secondo), con FsCheck (FsCheck.Xunit 3.4.0, BSD-3-Clause, solo nei progetti di test, con FSharp.Core
   sotto MIT). I crash vengono simulati a ogni passo di una scrittura.

## Verifica di piattaforma

1. Generico: integrità dello storage, nessun dominio.
2. Isolamento e sensibilità: non cambia chi può leggere cosa; i messaggi d'errore nominano un percorso, mai un contenuto.

## Criteri di accettazione

Gli stessi 10 di `spec.md`, nello stesso ordine.
