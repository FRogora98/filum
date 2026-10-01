# Il nucleo della memoria: un registro di eventi e le proiezioni che l'agente costruisce

> Stato: Rivista (il proprietario ha approvato ogni risposta proposta, 2026-10-01: "a me tornano tutte le cose che mi dici")
> Area: engine (il nucleo della memoria) / il turno
> Autore/data: federico rogora (redatta da Claude), 2026-10-01
> Roadmap: fase E; la decisione del proprietario del 2026-10-01 di ripensare il nucleo partendo dall'architettura, tenendo il telaio ("il nucleo è il vero motore dell'engine, se cambiamo quello il resto può rimanere")

## Problema

Oggi la memoria di Filum è quello che l'agente scrive mentre risponde: un riassunto, scritto nel turno. La prima esecuzione di LongMemEval (spec 020) ha misurato i due costi di questo disegno:
- **Perde.** I modelli piccoli hanno risposto giusto nel 65–85% dei casi dalla memoria di Filum, contro il 95–100% dalle sessioni grezze. Si perdono date, numeri e la vecchia versione di un fatto che è cambiato.
- **Costa.** Ogni informazione passa per un turno completo dell'agente con chiamate di strumenti, circa 15 volte il costo di tenere le sessioni.

Cercare nelle conversazioni salvate (spec 029) ha portato Gemma dal 65% all'80%: quello che è stato detto, se conservato, è la memoria più forte che ci sia.

Il personal agent del proprietario, il modello del comportamento di Filum, mostra la stessa forma dall'interno (il suo stesso resoconto, 2026-10-01):
- non tiene le conversazioni originali, solo quello che ha estratto, quindi un'estrazione sbagliata è persa per sempre;
- non ha consolidamento, quindi i fatti superati restano finché qualcuno non ci inciampa;
- la sua tabella di instradamento ("argomento → file") è tenuta a mano, quindi un file senza riga è un file perso;
- i suoi file di stato crescono senza un riassunto da leggere al loro posto;
- i fatti stabili finiscono nel diario invece che nel profilo.

Quello che fa bene è quello che Filum deve tenere:
- l'agente si crea le proprie strutture (tabelle con uno schema e una colonna `note` libera), procedure e script;
- una storia datata e solo in aggiunta;
- lo stato tenuto separato dalla storia;
- ogni risposta cita la sua fonte.

## Obiettivo

Una memoria in cui niente di quello che è stato detto va mai perso, e le strutture dell'agente sono costruite da lì, si possono ricostruire da lì e dicono da dove vengono:
- **un registro di eventi**, solo in aggiunta, l'unica fonte di verità;
- **proiezioni** che l'agente crea e fa evolvere sopra di esso: il nucleo, i fatti che valgono per un periodo, le collezioni, i documenti, le skill e, più avanti, le funzioni.

Si comporta come il personal agent del proprietario, senza le sue perdite: si adatta alla persona, estrae e aggiorna le sue strutture dal vivo quando un messaggio lo richiede, e si crea le sue collezioni, task e procedure (il proprietario, 2026-10-01: "un sistema che agisca esattamente COME personal agent, ma non necessariamente nello stesso modo"). Il costo conta, ma è un obiettivo fra gli altri, non il principale. Il telaio resta com'è: hosting, endpoint, `filum-mcp`, eval, pacchetti, il claim check.

## Non obiettivi

- **Nessun cambio al telaio:** hosting (017), il contratto degli endpoint (019), i pacchetti (016), il runner delle eval (018, 020) e l'installazione di `filum-mcp` restano. Cambia quello che trasportano.
- **Niente embedding in questa spec:** la ricerca resta lessicale con filtri di tempo. La ricerca ibrida (023) e il filtro veloce (022) si innestano dopo sul percorso di lettura e si misurano contro di esso.
- **Ancora niente funzioni:** il codice proprio dell'agente (capacità 5) è un tipo di proiezione successivo. Il registro e il modello delle proiezioni sono pensati perché ci stia.
- **Niente connettori esterni:** alimenteranno il registro più avanti (024). Questa spec ha bisogno solo delle conversazioni e delle importazioni da file.

## Comportamento attuale

- **`MemoryService`:** file con revisioni, scritti dagli strumenti dell'agente durante il turno.
- **Il nucleo `/filum.md`:** sempre nelle istruzioni, con un indice di ogni file.
- **Le conversazioni** sono salvate dal turno hosted e consultabili dalla 029 (spenta di default). `filum-mcp` non vede nessuna conversazione.
- **Consolidamento:** nessuno. I fatti vengono sovrascritti sul posto o aggiunti in coda, e il valore corrente di un fatto che cambia è quello che ha lasciato l'ultima scrittura.

## Comportamento desiderato

### 1. Il registro di eventi: quello che è successo, mai perso

Ogni persona ha un registro di eventi, solo in aggiunta. Un evento ha:
- un id;
- **quando è successo** (`occurred_at`) e **quando è stato registrato** (`recorded_at`): una mail importata o una sessione passata sono successe prima di essere registrate;
- il suo **tipo** e la sua **fonte** (una chat e il suo messaggio, un'importazione e il suo elemento, la persona nell'app, l'agente, il consolidamento);
- il suo testo o i suoi dati;
- la sua sensibilità.

I tipi:

| Tipo | Esempio |
|---|---|
| `said` | un messaggio della persona |
| `answered` | la risposta dell'agente |
| `imported` | un elemento da un file o, più avanti, da un connettore (l'export di una chat, una mail, un appuntamento) |
| `told` | un'istruzione esplicita: "ricordati…", "d'ora in poi…", "dimentica…" |
| `corrected` | la persona cambia, nasconde, cancella o annulla qualcosa nell'app |
| `derived` | una modifica a una proiezione, con gli eventi da cui viene |

Niente nel registro viene mai riscritto. Cancellare è un evento `corrected`. Una cancellazione vera (il diritto all'oblio) rimuove gli eventi che nomina e tutto quello che ne deriva, e lo dichiara.

### 2. Le proiezioni: quello che l'agente costruisce

Una proiezione è una struttura derivata dagli eventi, leggibile e modificabile come i file di oggi e versionata come loro (revisioni, annulla). Ognuna ha:
- il suo tipo;
- **un riassunto**, poche righe da leggere al posto dell'intera proiezione quando bastano;
- gli eventi da cui è derivata.

| Proiezione | Cosa contiene | La lezione dietro |
|---|---|---|
| **nucleo** | chi è la persona, le sue regole, come rispondere | i fatti stabili vanno qui, non in un diario |
| **fatti** | affermazioni che valgono per un periodo, tenute in una collezione normale `/facts.csv` (soggetto, attributo, valore, `valid_from`, `valid_to`, fonti, nota) che la persona può aprire e modificare come qualunque altra. Il valore corrente è una ricerca, non una sovrascrittura: un aggiornamento chiude il fatto vecchio e ne apre uno nuovo | il caso delle 4 piante invece di 3; sezioni "Stato al …" che si contraddicono |
| **collezioni** | tabelle con uno schema scelto dall'agente, sempre una colonna `note` libera, una data per ogni riga e la fonte di ogni riga | i CSV del personal agent |
| **documenti** | note narrative con sezioni datate | decisioni, piani, riflessioni |
| **skill** | procedure, come oggi | `/spesa`, `/sprint` |
| *(più avanti)* **funzioni** | il codice dell'agente, in sandbox | gli script del personal agent |

**La mappa** ("argomento → proiezione") è generata dai riassunti delle proiezioni, mai tenuta a mano. Insieme al nucleo e alle skill attive è quello che l'agente legge sempre.

### 3. Scrivere: dal vivo quando conta, mai perso, riordinato dopo

- **Registrato, sempre:** ogni messaggio è un evento, prima che giri qualsiasi altra cosa. Non si perde niente se l'agente non scrive niente, o scrive male.
- **Noto dal messaggio successivo:** quello che la persona dice non aspetta mai il consolidamento.
  - Nella stessa conversazione ogni messaggio precedente fa parte del turno (come oggi: viene inviata l'intera conversazione).
  - In una nuova conversazione è noto perché il turno l'ha scritto.

  "Mi chiamo …" è noto al messaggio dopo e nella chat dopo.
- **Dal vivo, quando il messaggio lo richiede,** come fa il personal agent:
  - nel turno l'agente aggiorna le sue proiezioni: un fatto nuovo (chiudendo quello che sostituisce), una riga, un task, una sezione datata, una regola nel nucleo, una nuova collezione o skill che propone;
  - ognuna di queste scritture cita gli eventi da cui viene;
  - il claim check verifica che quello che è stato dichiarato sia stato fatto.

  "Quando lo richiede" lo giudica l'agente, guidato dalle istruzioni: un'istruzione duratura, una correzione, un fatto sulla persona o sulla sua vita, una richiesta di tenere qualcosa. Le chiacchiere non scrivono niente.
- **Il consolidamento** è la rete di sicurezza, ed è la parte che al personal agent manca:
  - **Quando:** dopo 10 minuti senza nuovi messaggi in una conversazione, in un passaggio notturno e su richiesta. Legge gli eventi successivi al passaggio precedente. Quando gira non cambia niente di quello che l'agente sa, solo quanto è ordinato.
  - **Cosa fa:**
    - recupera quello che il turno non ha scritto: trasforma gli eventi in fatti (chiudendo i fatti che sostituiscono), righe e sezioni datate;
    - aggiorna i riassunti e la mappa;
    - unisce i duplicati;
    - segnala le contraddizioni che non riesce a risolvere.
  - **Chi lo fa girare:** nel prodotto hosted, un modello economico (più avanti il filtro veloce, dove basta). In `filum-mcp`, che non ha un modello suo, il modello dell'ospite, quando Filum glielo chiede (§5).
  - **Come si vede:** ogni modifica è un evento `derived`, visibile nella cronologia e annullabile.
  - **I cambi di struttura** (una nuova collezione, una nuova skill, una nuova sezione del nucleo) vengono **proposti** alla persona, come oggi le skill. Righe, fatti e riassunti vengono applicati direttamente. La regola del personal agent, "quando non so dove va una cosa, chiedo", diventa una proprietà del sistema.
- **Conversazioni lunghe:** oltre la lunghezza che il modello riesce a leggere intera, i messaggi più vecchi della conversazione vengono inviati come riassunto. Restano tutti nel registro, e `events_search` li trova.
- **Una proiezione modificata dalla persona** registra un evento `corrected`: vince la sua versione, e il consolidamento non la sovrascrive mai.

### 4. Leggere: instradare, poi citare

- **L'agente legge sempre** il nucleo, la mappa e le skill attive.
- **Gli strumenti, per quello a cui rispondono:**
  - `memory_overview`, come oggi, dalla mappa;
  - `facts_current` e `facts_history`: cosa vale ora, e come è cambiato;
  - `events_search`: cosa è stato detto o importato, con filtri di tempo su `occurred_at`. Sostituisce ed estende `conversation_search`;
  - gli strumenti di oggi sulle proiezioni: leggere una proiezione, aggregare una collezione con esattezza.
- **Ogni risposta che riporta un fatto ricordato nomina la sua fonte** (una proiezione o un evento) nei passi del turno. Il claim check impara a chiedersi "ogni affermazione ricordata è sostenuta da un passo?": è il "cita sempre il file fonte" del personal agent, verificato nel codice.

### 5. I due prodotti

- **Il prodotto hosted:** il registro è una tabella accanto alle proiezioni, alimentata dal turno e dalle importazioni.
- **`filum-mcp`:** Filum non ha un modello e non vede la conversazione. Tutto il ragionamento lo fa il modello dell'ospite, attraverso gli strumenti di Filum.
  - Il registro è un file solo in aggiunta nella cartella (`.filum/events.jsonl`), accanto alle proiezioni, che sono i file di oggi.
  - **Le scritture dal vivo** sono il modello dell'ospite che chiama gli strumenti delle proiezioni, come oggi.
  - **`memory_log`** registra quello che la persona ha detto di importante, come lo sceglie il modello dell'ospite: un evento aggiunto, nessuna proiezione scritta.
  - **`memory_consolidate`** passa al modello dell'ospite gli eventi non ancora consolidati, con quello che il consolidamento deve fare (§3); il modello lo fa con gli strumenti di sempre. Le istruzioni dell'MCP gli chiedono di chiamarlo a inizio sessione, quando ci sono eventi in sospeso, e ogni volta che la persona lo chiede. Il costo è dell'ospite, come tutto il resto.
  - Le istruzioni chiedono all'ospite di registrare quello che conta, e le importazioni da file alimentano il registro come nel prodotto hosted. Il test di crescita misura quanto spesso gli ospiti lo fanno davvero.
  - Il sampling MCP (il server che chiede al client di far girare un modello) non è un requisito: il supporto varia da client a client.

### 6. Sostituisce il nucleo di oggi

Filum è ancora in sviluppo: non è stata rilasciata nessuna memoria stabile. Il nucleo descritto qui prende il posto di quello di oggi pezzo per pezzo, e ogni pezzo toglie, nella stessa modifica, il codice che sostituisce. Non c'è uno strato di compatibilità né un periodo con due nuclei. Il telaio funziona a ogni commit.

### 7. Come si decide

Su LongMemEval (spec 020), su `oracle` e su `S`, con gli stessi sottoinsiemi e gli stessi modelli:
- **I sistemi:** questo nucleo e i riferimenti (nessuna memoria, ricerca semplice sulle sessioni grezze), con le misure della spec 020 come punto di partenza.
- **Le ablazioni:** senza consolidamento, senza validità dei fatti, senza la mappa generata.
- **L'obiettivo:** vicino alle sessioni grezze (95–100% su `oracle` nella spec 020), almeno sugli aggiornamenti di conoscenza e sulle domande temporali, con un costo ragionevole per domanda. Se resta sotto, il disegno si ripensa prima di costruirci sopra altro.

## Verifica di piattaforma

1. **Generico:** sì. Eventi, fatti, collezioni, documenti e skill non nominano nessun dominio. Le strutture di un dominio sono quelle che un agente costruisce dagli eventi di una persona, o che un pacchetto semina.
2. **Sensibilità e privacy:**
   - gli eventi hanno una sensibilità, e ogni proiezione derivata da un evento privato è privata;
   - una cancellazione vera segue le fonti fino a ogni proiezione costruita da esse, cosa che una memoria che perde non potrebbe fare;
   - niente di personale entra in questo repository.

## Criteri di accettazione

1. Dato un turno, quando la persona scrive, allora viene registrato un evento `said` con il suo `occurred_at`, prima che giri qualsiasi strumento, e la risposta come evento `answered`.
2. Data una persona che dice "mi chiamo Sam", quando scrive di nuovo nella stessa conversazione e quando apre subito dopo una nuova conversazione, allora l'agente conosce il nome in entrambi i casi, senza nessun consolidamento nel mezzo.
3. Data un'istruzione esplicita ("d'ora in poi…"), quando il turno finisce, allora il nucleo la contiene, la sua fonte è un evento `told` e il claim check l'ha verificata.
4. Data una conversazione finita, quando gira il consolidamento, allora i suoi nuovi fatti, righe e sezioni datate esistono con i loro eventi fonte, e ogni modifica è un evento `derived`, annullabile.
5. Dato un fatto che cambia ("ora 4, non 3"), quando il consolidamento vede il nuovo evento, allora il fatto vecchio riceve `valid_to` e quello nuovo `valid_from`; `facts_current` dà 4, e `facts_history` li dà entrambi con le loro fonti.
6. Dato un tipo di informazione senza una casa, quando il consolidamento creerebbe una collezione, allora la propone alla persona, e niente viene creato finché la persona non accetta.
7. Data una proiezione modificata dalla persona, quando il consolidamento gira di nuovo, allora resta la versione della persona.
8. Data qualunque proiezione, quando si legge la mappa, allora ha una riga per proiezione con il suo riassunto, generata, mai tenuta a mano.
9. Data una persona che cancella per sempre una fonte, quando gira la cancellazione, allora i suoi eventi e tutto quello che deriva solo da essi spariscono, e la cronologia registra la cancellazione.
10. Dato `filum-mcp`, quando l'ospite chiama `memory_log`, allora un evento viene aggiunto a `.filum/events.jsonl` e nessuna proiezione viene scritta nel turno.
11. Dato `filum-mcp` con eventi non ancora consolidati, quando l'ospite chiama `memory_consolidate`, allora riceve quegli eventi e le istruzioni per consolidarli, e dopo che l'ospite ha scritto, la chiamata successiva non restituisce niente in sospeso.
12. Dati LongMemEval `oracle` e `S`, quando questo nucleo e i riferimenti vengono giocati sugli stessi sottoinsiemi e modelli, allora il report mostra l'accuratezza per abilità e il costo per domanda di ciascuno, con le ablazioni, e decide l'obiettivo del §7.
13. Data una conversazione più lunga di quanto il modello riesca a leggere, quando la persona chiede dei suoi primi messaggi, allora l'agente li trova con `events_search`, e niente della conversazione manca dal registro.

## Decisioni

Il proprietario ha risposto a tutte le domande aperte il 2026-10-01 ("a me tornano tutte le cose che mi dici"):
- **La memoria accumulata finora nel dogfooding:** si butta quando il nuovo nucleo va in produzione (è quasi vuota).
- **Quando gira il consolidamento:** dopo 10 minuti senza nuovi messaggi, di notte e su richiesta (§3).
- **Proposto o applicato:** collezioni, skill e sezioni del nucleo vengono proposte; fatti, righe, sezioni datate e riassunti vengono applicati, tutto annullabile (§3).
- **Fatti:** una collezione normale, `/facts.csv` (§2).
- **`filum-mcp`:** il modello dell'ospite registra con `memory_log` e consolida con `memory_consolidate` quando Filum glielo chiede (§5).
- **Conversazioni lunghe:** i messaggi più vecchi vengono riassunti per il modello, e restano tutti nel registro (§3).
