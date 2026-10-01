# Eval per qualunque host

> Stato: Implementata
> Area: eval
> Autore/data: federico rogora (redatta da Claude), 2026-09-30
> Roadmap: 018, sincronizzata con il primo prodotto che ospita il motore (punto di sincronizzazione S5)

## Problema

La promessa del motore, "una memoria su cui qualunque agente può contare, anche con modelli piccoli", vale quanto le sue misure. Le misure esistono: un runner di eval gioca scenari sintetici contro un servizio in esecuzione e dà a ogni modello un punteggio su compiti riusciti, modifiche dichiarate ma non fatte, regole rispettate, costo e velocità. È così che si è scelto il modello di default. Ma il runner ha tre limiti:
- **È privato e legato a un solo host.** Vive nel repository del prodotto ospitato e usa le rotte di quel prodotto (`/api`), la sua registrazione degli account e la sua lista di modelli.
- **I suoi scenari vivono dentro di lui,** quindi un prodotto non può tenerne di propri. Il primo host ha bisogno di scenari di guardrail (niente cancellazioni, niente consulenza regolamentata, modifiche solo proposte) che appartengono al suo repository, non a questo.
- **Niente misura il prodotto open source stesso:** un agente con `filum-mcp` montato ricorda meglio, da una sessione all'altra, dello stesso agente senza?

Il proprietario ha deciso il 2026-09-30 che il runner diventa pubblico ("Pubblico") e che il confronto dell'MCP gira con l'agente del proprietario, a mano ("Con claude -p, a mano").

## Obiettivo

Un solo runner di eval pubblico che:
- gioca una cartella di scenari, da qualunque posto, contro qualunque servizio che ospita il motore, con le rotte, il login e il modello dell'host;
- controlla i guardrail oltre alla memoria;
- misura `filum-mcp` giocando gli stessi scenari in sessioni nuove di un agente vero, con e senza Filum montato.

## Non-obiettivi

- **Mai in CI e mai in `dotnet test`:** il runner chiama modelli a pagamento. La sua logica si testa senza di loro.
- **Nessuno scenario di un prodotto qui:** solo quelli sintetici, senza dominio. Un prodotto tiene i suoi nel suo repository.
- **Nessun risultato pubblicato in automatico.** Un report viene scritto in locale; pubblicarne uno è un commit voluto.
- **Nessun nuovo modello giudice:** il giudice resta `gpt-5.4-mini`, fisso, e legge solo la risposta.
- **Nessun cambiamento al motore, al turno o ai loro strumenti.**

## Comportamento attuale

- **`Filum.Evals`** (repository del prodotto ospitato, circa 700 righe):
  - `ScenarioRunner` registra un account su `/api/auth/register`, manda ogni turno a `/api/conversations/{id}/messages` con il modello in prova, legge la memoria da `/api/memory/*`, controlla nel codice le aspettative di ogni turno, e chiede al giudice se la risposta dichiara una modifica;
  - `EvalRun` esegue ogni scenario × modello × ripetizione entro un tetto di spesa, ed `EvalReport` scrive `report.md` e `results.json`.
- **Gli scenari** sono file JSON in `Filum.Evals/scenarios/` (17, sintetici, in inglese e in italiano). I tipi di aspettativa sono `wrote`, `no_write`, `step`, `answer_contains`, `answer_not_contains`, `answer_max_words`, `core_contains`, `core_not_contains`, `memory_contains`, `file`, `rows`, `skill`, `no_skill`, `used_skill` e `proposal`.
- I modelli da confrontare devono essere in `/api/models` del servizio.

## Comportamento desiderato

**Il runner in questo repository.** Vive in `src/Filum.Evals`, con i 17 scenari sintetici in `evals/scenarios/`. Il prodotto ospitato non ne tiene una copia: esegue questo runner contro il suo servizio, con gli stessi risultati di oggi.

**Qualunque host.** Il bersaglio si indica da riga di comando:
- `--service <url>`, e `--prefix <percorso>` per dove l'host ha montato i gruppi (default `/api`).
- **Chi è la persona sintetica**, una persona nuova per ogni esecuzione, così le esecuzioni non condividono mai la memoria:
  - `--register <percorso>`: registra un account con il contratto di autenticazione comune (email e password in ingresso, access token in uscita), come fanno sia il prodotto ospitato sia il primo host;
  - `--person-header <nome>`: manda l'id di una persona nuova in quell'header, per host come quello di esempio.
- `--setup "<comando>"` esegue un comando dell'host dopo che la persona è stata creata, con `{email}` e `{person}` sostituiti, per esempio per dare a un account nuovo il piano che serve a uno scenario. Un comando che fallisce fa fallire l'esecuzione, prima di qualunque turno. È un comando dell'host sulla macchina dell'host; il runner non aggiunge nessuna rotta a nessun host.
- `--models <id>` confronta modelli del gruppo `models` dell'host. `--models host` usa il modello dell'host, per gli host che non lasciano scegliere a una richiesta; il report lo chiama "host".
- `--scenarios-dir <cartella>` gioca gli scenari di qualunque cartella, e `--scenarios <pattern>` li filtra per id.
- **Un host che non monta il gruppo della memoria** si può valutare comunque: le aspettative sulla memoria falliscono con "the host maps no memory group", e le altre si controllano come al solito.

**Aspettative di guardrail**, nuovi tipi, controllati nel codice tranne dove un giudice deve leggere il linguaggio:
- `tool` / `no_tool` (`any`: nomi di strumenti): c'è stato un passo di uno di questi strumenti, oppure nessuno. Contano anche gli strumenti dell'host, perché le loro chiamate sono passi.
- `surfaced` (`any`: nomi di strumenti): un passo di uno di questi strumenti porta dati in evidenza. Facoltativi:
  - `contain`: testi contenuti nei dati;
  - `fields`: campi presenti e non vuoti;
  - `equals`: campo → valore.
- `refused` (`count` facoltativo: uno stato HTTP): l'host ha rifiutato il turno, per esempio con il suo cancello del turno, e niente del turno è stato salvato (la conversazione che avrebbe aperto non esiste).
- `host_unchanged` (`get`: un percorso dell'host): il runner legge quel percorso, come la persona, prima e dopo il turno, e le due risposte sono uguali. È la prova che non si è scritto niente, qualunque strumento abbia usato il modello, anche uno che nessuno scenario elenca.
- `judge` (`question`, `pass_if`: `yes` o `no`): il giudice fisso risponde alla domanda sì/no dello scenario sulla risposta, per esempio "La risposta consiglia di comprare un prodotto finanziario specifico?" con `pass_if: no`.
  - Il costo del giudice si conta nell'esecuzione e ha un tetto come il resto.
  - La domanda è un dato, nello scenario del prodotto; il motore non nomina nessun dominio.

**Il growth test dell'MCP** (modalità `mcp`, a mano):
- Gioca gli scenari con un agente vero attraverso la sua riga di comando, una sessione nuova per ogni turno: il primo agente è Claude Code (`claude -p`), con l'abbonamento del proprietario.
- **Due bracci,** ognuno con una cartella temporanea:
  - *con Filum*: solo `filum-mcp`, su un `FILUM_HOME` nuovo;
  - *senza*: nessun server MCP.

  In entrambi i bracci gli strumenti di file e di memoria dell'agente sono spenti, così solo Filum può ricordare. È la lezione della spec 013, dove aveva risposto la memoria integrata dell'agente.
- **Cosa si controlla:**
  - le risposte si controllano come al solito;
  - le aspettative sulla memoria leggono la cartella di Filum con il motore stesso;
  - i passi vengono dalle chiamate di strumenti dell'agente (uno strumento di Filum che scrive conta come `wrote`).
- **Limiti:**
  - `--max-sessions` limita il numero di sessioni dell'agente, stimato prima dell'esecuzione e imposto durante;
  - il costo del giudice ha un suo tetto.
- Il report mette i due bracci uno accanto all'altro, scenario per scenario.

**I report** restano come sono (`report.md`, `results.json`). In più:
- una colonna "host" per il bersaglio;
- il numero dei turni che hanno risposto e di quelli rifiutati, per modello, così un host che conta i messaggi (per esempio come crediti) vede quanto gli costa un'esecuzione;
- in modalità `mcp`, i due bracci.

## Verifica di piattaforma

1. **Generico:** sì.
   - Il bersaglio, il modo di login e gli scenari sono configurazione e dati.
   - I nuovi tipi di aspettativa non conoscono nessun dominio, e una domanda al giudice è un dato di uno scenario.
   - Gli scenari qui restano sintetici.
2. **Sensibilità e riservatezza:**
   - ogni esecuzione usa una persona sintetica nuova (`@example.invalid` o un id casuale) e cartelle temporanee;
   - il giudice vede solo le risposte;
   - la chiave del giudice e il login dell'agente si leggono dall'ambiente e non vengono mai scritti in un report;
   - nessuno scenario o risultato di un prodotto entra in questo repository.

## Criteri di accettazione

1. Dato questo repository, quando girano i suoi test (nessun modello, nessuna chiave), allora passano il caricatore degli scenari, ogni tipo di aspettativa (vecchi e nuovi) e il flusso del runner contro l'host di esempio con un modello finto.
2. Dato il servizio del prodotto ospitato, quando il runner gioca i 17 scenari con `--service`, `--prefix /api` e `--register /api/auth/register`, allora gira come prima, e il prodotto ospitato non tiene nessuna copia del runner o degli scenari.
3. Dato l'host di esempio, quando il runner gioca uno scenario con `--person-header X-Sample-Person` e `--models host`, allora ogni esecuzione è una persona nuova, e il report chiama il modello "host".
4. Data una cartella di scenari fuori da questo repository, quando viene passata con `--scenarios-dir`, allora i suoi scenari vengono giocati; un tipo di aspettativa sconosciuto ferma l'esecuzione prima di qualunque turno, nominando il file e il turno.
5. Dato un host che non monta il gruppo della memoria, quando si gioca uno scenario con aspettative sulla memoria e sulla risposta, allora quelle sulla memoria falliscono con "the host maps no memory group" e quelle sulla risposta vengono controllate.
6. Dati i tipi `tool`, `no_tool`, `surfaced` (con `contain`, `fields` ed `equals`), `refused`, `host_unchanged` e `judge`, quando si controllano su turni registrati, allora ognuno passa e fallisce come specificato; `judge` chiama il giudice con la domanda dello scenario, e il suo costo viene contato.
7. Data la modalità `mcp` con `--max-sessions` sotto quanto serve all'esecuzione, quando parte, allora rifiuta a meno che la si forzi; durante un'esecuzione si ferma al tetto e riporta ciò che ha completato.
8. Data la modalità `mcp`, quando gira, allora il braccio senza Filum non ha nessun server MCP e nessuno strumento di file o di memoria, il braccio con Filum ha solo `filum-mcp` su una cartella nuova, e il report mostra entrambi i bracci per scenario. È una verifica a mano, fatta una volta e scritta nei task.
9. Dato `--setup` con un comando, quando parte un'esecuzione, allora il comando gira una volta per ogni persona nuova, con `{email}` e `{person}` sostituiti, prima del primo turno; un comando che esce con un errore ferma quell'esecuzione, con il suo output nel report.
10. Data un'esecuzione, quando si scrive il report, allora mostra, per modello, i turni che hanno risposto e quelli rifiutati.

## Domande aperte

Nessuna. Risposte del proprietario del 2026-09-30:

- Il runner diventa pubblico ("Pubblico").
- Il confronto dell'MCP usa `claude -p`, a mano ("Con claude -p, a mano").
