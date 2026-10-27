# Pacchetti: un verticale fatto di dati

> Stato: Implementata
> Area: motore / MCP
> Autore/data: federico rogora (redatta da Claude), 2026-09-30
> Roadmap: 016, sincronizzata con il primo prodotto che ospita il motore (il suo pacchetto è il primo vero)

## Problema

Il motore è generico per scelta: non conosce nessun dominio. Un prodotto che vuole un agente verticale (un coach su una materia, un assistente per un tipo di lavoro) deve far partire ogni memoria nuova con le sue indicazioni, con le procedure che sa fare dal primo giorno e con i tipi di elenchi che tiene.

Oggi l'unico modo è il codice: le skill iniziali sono una costante in `Skills.Starters`, il template del nucleo è `PlatformInstructions.CoreTemplate`, e per le regole proprie di un prodotto non c'è nessun posto. La regola di prodotto vieta di mettere un dominio nel codice, quindi il verticale deve essere fatto di dati, caricati per configurazione, con il motore invariato.

## Obiettivo

Un pacchetto è una cartella di file semplici. Un host (`filum-mcp`, o un prodotto che ospita il motore) indica al motore dove si trova, e da lì in poi ogni memoria nuova parte con i file e le skill del pacchetto, e l'agente segue le regole del pacchetto. Senza pacchetto il motore è il Filum generico, esattamente come oggi.

## Non-obiettivi

- **Nessuno strumento di dominio:** un pacchetto contiene solo dati. Gli strumenti di un dominio sono codice dell'host (spec 017) oppure, dentro un agente, altri server MCP montati dall'host.
- **Nessun aggiornamento delle memorie esistenti:** un pacchetto si applica quando una memoria viene creata. Cosa succede alle memorie create con una versione più vecchia è una spec successiva (vedi la domanda aperta).
- **Nessun registro di pacchetti, download o firma:** un pacchetto è una cartella che l'host ha già.
- **Non più di un pacchetto alla volta.**
- **Nessun vero pacchetto verticale in questo repository:** solo un esempio minimo e senza dominio. I pacchetti veri vivono con i prodotti a cui appartengono.

## Comportamento attuale

- `MemoryService.EnsureCoreAsync` (`src/Filum.Engine/MemoryService.cs`) crea, la prima volta, il nucleo `/filum.md` da `PlatformInstructions.CoreTemplate` e salva le `Skills.Starters` come file `/skills/<nome>.md`.
- I file delle skill hanno un formato fisso, letto e controllato da `Skills` (`src/Filum.Engine/Skills.cs`).
- Una collezione è un file `.csv` la cui riga di intestazione è il suo schema.
- Le istruzioni di un turno sono lo strato di piattaforma (`PlatformInstructions.Text`, oppure `McpInstructions.Text` in `filum-mcp`), poi il nucleo della persona, l'indice e le skill. Il nucleo "si aggiunge a queste istruzioni e non le sovrascrive mai".

## Comportamento desiderato

**Il formato.** Un pacchetto è una cartella:

```
pack.json        obbligatorio: {"format": 1, "name": "journal", "version": "1.0.0", "description": "Una riga."}
prompt.md        facoltativo: le regole e le indicazioni del pacchetto per l'agente, testo semplice
skills/*.md      facoltativo: skill iniziali, nel formato dei file delle skill
memory/**        facoltativo: i file con cui parte una memoria nuova, agli stessi percorsi
                 (documenti .md, collezioni .csv con la loro intestazione, /filum.md al posto del template del nucleo)
```

- `format` è la versione del formato dei pacchetti: questa spec definisce la 1. Un pacchetto con un formato che il motore non conosce viene rifiutato con un messaggio chiaro, mai letto a metà. `version` è la versione del pacchetto stesso, per chi lo scrive.
- `pack.json` può elencare `"private": ["/percorso", …]` e `"sensitive": [...]`: file di `memory/` creati con quella sensibilità.
- Tutto viene controllato con le regole del motore, prima di toccare qualunque memoria:
  - i file delle skill si leggono, e i loro nomi sono validi;
  - i percorsi sono percorsi di memoria validi;
  - le collezioni hanno un'intestazione;
  - ogni file rispetta i limiti di dimensione;
  - i file, le skill iniziali del motore e le skill del pacchetto, insieme, rispettano il numero massimo di file.
- Un pacchetto con un qualunque problema viene rifiutato per intero, con l'elenco di tutti i problemi. Non se ne usa niente.

**Il caricamento.**
- `filum-mcp` legge `FILUM_PACK` (il percorso di una cartella).
- Un host passa la cartella al motore con la sua configurazione, per esempio `Memory:PackPath`.
- All'avvio l'host scrive nel log il nome e la versione del pacchetto caricato (da `pack.json`), così un'istanza in esecuzione mostra quale pacchetto usa.
- Un pacchetto che manca o viene rifiutato ferma l'host all'avvio, con l'elenco dei problemi su stderr o nel log. Filum non gira mai con mezzo pacchetto, e mai senza pacchetto senza dirlo.

**Una memoria nuova con un pacchetto.** Quando si crea la memoria di una persona:
- il nucleo viene da `memory/filum.md` del pacchetto se c'è, altrimenti dal template;
- i file di `memory/` del pacchetto vengono creati con l'autore "platform", con la sensibilità dichiarata;
- vengono salvate le skill iniziali del motore, poi quelle del pacchetto; una skill del pacchetto con lo stesso nome di una iniziale la sostituisce;
- la storia delle revisioni mostra tutto questo come opera della piattaforma, quindi si può annullare come qualunque altra cosa.

**Il prompt del pacchetto.** Sta subito dopo lo strato di piattaforma e prima del nucleo della persona, con la stessa autorità: il nucleo della persona vi si aggiunge e non lo sovrascrive mai. Così le regole non negoziabili di un prodotto valgono qualunque cosa la persona scriva nel suo nucleo.
- Il prompt è la prima linea dei guardrail di un prodotto, non l'unica: ciò che non deve mai succedere lo impone il codice dell'host (spec 017), per esempio strumenti esclusi con `ExcludedTools`, o proposte che la persona conferma al posto delle scritture.
- Nel loop ospitato fa parte delle istruzioni di ogni turno.
- In `filum-mcp` si aggiunge alle istruzioni del server, e `memory_overview` lo restituisce in cima.

**Il resto non cambia.** Gli strumenti, i loro nomi e le loro descrizioni sono gli stessi, e lo snapshot del catalogo non si muove.
- Una memoria creata prima che il pacchetto venisse impostato tiene i suoi file e le sue skill.
- Il prompt si legge dal pacchetto, non viene mai copiato in una memoria, quindi vale per ogni memoria dal turno successivo.

**Il pacchetto di esempio.** `packs/example/` in questo repository è piccolo e senza dominio:
- un prompt di poche righe;
- una skill iniziale;
- una collezione con intestazione;
- un documento.

Lo usano i test e il README.

## Verifica di piattaforma

1. **Generico:** sì. Il formato e il caricatore non conoscono nessun dominio; il dominio sta solo nei dati che fornisce un prodotto. L'esempio non ne nomina nessuno.
2. **Sensibilità e riservatezza:**
   - i file del pacchetto hanno la sensibilità che il pacchetto dichiara, e il motore la applica come per ogni file;
   - un pacchetto non contiene dati personali: è uguale per tutti, e viene applicato a una memoria solo quando quella memoria viene creata;
   - niente di privato entra in questo repository: l'esempio è sintetico.

## Criteri di accettazione

1. Dato il pacchetto di esempio, quando si crea una memoria nuova con esso, allora contiene il nucleo del pacchetto o il template, i suoi file ai loro percorsi con la sensibilità dichiarata, le skill iniziali del motore e quelle del pacchetto, tutti con revisioni della piattaforma.
2. Senza pacchetto, quando si crea una memoria nuova, allora è esattamente come oggi: il nucleo dal template e le skill iniziali.
3. Dato un pacchetto con un file di skill rotto, un percorso non valido e una collezione senza intestazione, quando viene caricato, allora viene rifiutato, i tre problemi vengono elencati e nessuna memoria viene toccata.
4. Dato un pacchetto con un prompt, quando si compongono le istruzioni di un turno (loop ospitato) o parte `filum-mcp`, allora il prompt viene dopo lo strato di piattaforma e prima del nucleo della persona. `memory_overview` comincia con il prompt.
5. Dato un nucleo della persona con una regola che contraddice il prompt del pacchetto, quando si compongono le istruzioni, allora il testo di piattaforma dice ancora che il nucleo non sovrascrive mai ciò che viene prima.
6. Data una memoria creata prima che un pacchetto venisse impostato, quando il pacchetto viene impostato, allora i file e le skill di quella memoria restano invariati, e il prompt del pacchetto vale per i suoi turni successivi (il prompt si legge dal pacchetto, non viene mai copiato in una memoria).
7. Dato `FILUM_PACK` che punta a un pacchetto mancante o rifiutato, quando `filum-mcp` parte, allora esce con un codice diverso da zero e i problemi su stderr.
8. Dato lo snapshot del catalogo, quando si aggiungono i pacchetti, allora lo snapshot resta invariato.

## Domande aperte

Nessuna. Risposta del proprietario del 2026-09-30:

- **Le memorie che esistono già quando un pacchetto cambia versione:** rimandato ("ci penseremo più avanti"). Fino ad allora:
  - il prompt vale sempre, perché si legge a ogni turno e non viene copiato;
  - file e skill si applicano solo quando una memoria viene creata.

  Una spec successiva potrà aggiungere "dai alle persone esistenti ciò che non hanno ancora, non sovrascrivere mai ciò che hanno cambiato". Il proprietario pensa a un **porting agentico**: è l'agente stesso a portare la memoria di una persona alla nuova versione del pacchetto, tenendo le modifiche della persona ("io penserei ad un sistema di porting agentico").
