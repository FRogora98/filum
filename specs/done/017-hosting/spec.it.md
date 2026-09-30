# Ospitare il motore

> Stato: Implementata
> Area: motore / hosting
> Autore/data: federico rogora (redatta da Claude), 2026-09-30
> Roadmap: 017, progettata con il primo prodotto che ospita il motore (punto di sincronizzazione S3)

## Problema

Un prodotto che vuole un agente verticale con la memoria di Filum (il primo è un'app di finanza chiusa) deve far girare il motore dentro il proprio servizio. Ha il suo login, il suo database e strumenti di dominio che nessun pacchetto può esprimere come dati, e non vuole né un secondo servizio né un fork.

Oggi non può:
- **Il turno non è pubblico.** Il loop (`Filum.Agent`: istruzioni, agent loop, controllo di affidabilità, catalogo dei modelli e fornitori, conversazioni, consumi, storage su Postgres) vive solo nel repository privato del prodotto ospitato. Il proprietario ha deciso il 2026-09-30 che diventa pubblico ("Diventa pubblico").
- **Il turno offre solo gli strumenti del motore**, quindi un host non ha dove aggiungere i propri.
- **L'assistente si chiama Filum nel codice** ("You are Filum", il nome dell'agente), qualunque sia il prodotto.
- **Gli endpoint HTTP** (conversazioni, memoria, skill, modelli, consumi) appartengono a un solo host e un altro non può montarli.

## Obiettivo

Un prodotto ospita il motore come librerie di questo repository:
- registra i suoi strumenti accanto a quelli del motore;
- i risultati dei suoi strumenti possono viaggiare nei passi del turno, perché la sua app li mostri;
- dà un nome al suo assistente;
- tiene il suo login e il suo database;
- monta gli endpoint del motore dove vuole.

Un host di esempio in questo repository fa tutto questo, e il prodotto ospitato che usa già il motore diventa un host in più delle stesse librerie.

## Non-obiettivi

- **Nessun cambiamento agli strumenti del motore, ai loro nomi o al loro significato.** Lo snapshot del catalogo non si muove.
- **Niente account o login nelle librerie:** è l'host a decidere chi è la persona e a passarne l'id.
- **Ancora nessun contratto HTTP pubblico versionato:** gli endpoint si montano come sono oggi, e versionarli è la spec 019.
- **Niente eval per altri host:** spec 018.
- **Niente pacchetti NuGet:** gli host usano questo repository come submodule git, come fa già il prodotto ospitato. I pacchetti arrivano quando un host ne avrà bisogno.
- **Niente turni in streaming.**
- **Nessuno strumento o regola di un prodotto vero in questo repository:** solo quelli dell'host di esempio, senza dominio.

## Comportamento attuale

- **`Filum.Agent`** (repository privato, circa 1.400 righe, niente ASP.NET, nessun nome privato):
  - `ConversationService.SendAsync(userId, conversationId, request)` esegue un turno;
  - le istruzioni sono `PlatformInstructions.Compose(…)`, con il prompt del pacchetto dalla spec 016;
  - gli strumenti sono solo `MemoryTools.Tools`;
  - l'agente si chiama "Filum".
- **I passi** sono record `ToolStep` (tipo, strumento, percorso, descrizione, durata, revisione, errore, righe). Vengono salvati come JSON con ogni risposta e restituiti all'app.
- **Il database è già dell'host:** `FilumModel.Configure(modelBuilder)` aggiunge le tabelle del motore a un context dell'host, e l'host tiene le migrazioni; `IFilumDb` dà context di breve durata.
- **Gli endpoint** stanno in `Filum.AgentService/Api/*`, sotto `/api`. `RequireUserId()` legge l'utente dal JWT.
- `PlatformInstructions.Text` comincia con "You are Filum, the person's own assistant."

## Comportamento desiderato

**Le librerie in questo repository**
- **`src/Filum.Agent`:** il turno, spostato qui con namespace e comportamento invariati, MIT come il motore. Niente al suo interno nomina un prodotto o una macchina.
- **`src/Filum.Agent.Http` (nuova):** i gruppi di endpoint come metodi di estensione su un route group, con gli stessi percorsi e gli stessi payload di oggi:
  - conversazioni e messaggi, con le proposte di skill accettate o rifiutate;
  - file di memoria, revisioni e annullamento;
  - skill;
  - modelli;
  - consumi.

  Ogni gruppo si monta per conto suo: un host monta solo i gruppi che vuole, sotto il prefisso che vuole, e passa come leggere l'id della persona da una richiesta. Una richiesta senza persona riceve 401. Gli account restano in ogni host.
- **La scelta del modello** è dell'host: con `Agent:AllowModelChoice` spento, il modello indicato in una richiesta viene ignorato e ogni turno usa il modello di default dell'host.
- **Il servizio del prodotto ospitato** tiene i suoi account, il suo context e le sue migrazioni. Monta questi gruppi sotto `/api` con il suo JWT, così la sua app vede esattamente la stessa API.

**Strumenti dell'host**
- Un host registra strumenti per il turno con una **sorgente di strumenti del turno**. È un servizio scoped, risolto dallo scope della richiesta, così gli strumenti dell'host possono usare i suoi servizi scoped (per esempio un servizio dell'utente corrente e repository che filtrano per persona). Per ogni turno riceve un **contesto del turno**:
  - la persona, la conversazione e il messaggio;
  - il service provider della richiesta;
  - il cancellation token del turno, che arriva a ogni chiamata di strumento;
  - "adesso", dal `TimeProvider` dell'host;
  - il fuso orario della persona, come lo configura l'host (`Agent:TimeZone`, un nome IANA, default UTC).

  Restituisce strumenti costruiti per quel turno.
- I suoi strumenti vengono offerti al modello accanto a quelli del motore, e possono essere soggetti agli stessi `ExcludedTools`.
- Un nome che si scontra con uno strumento del motore, o con un altro strumento dell'host, ferma l'host all'avvio con un messaggio chiaro.
- Ogni chiamata a uno strumento dell'host diventa un **passo** del turno, come quelli del motore: il tipo, lo strumento, una descrizione di una riga, la durata, e un errore se è fallita.
- Uno strumento dell'host può restituire un **risultato in evidenza**: testo per il modello, più un piccolo valore JSON per l'app. Il valore si conserva con il passo (`data`) e torna con i passi della risposta, così l'app può mostrarlo, per esempio come card. Il modello non vede mai `data`, solo il testo.
- `data` è un nuovo campo facoltativo dei passi salvati e dei passi nel payload della risposta: si aggiunge soltanto, quindi un'app che non lo conosce lo ignora.
- `data` si salva con il passo, quindi torna anche quando si rilegge la conversazione, non solo nella risposta dal vivo. È al massimo 4 KB; un valore più grande rende la chiamata un passo fallito ("il risultato era troppo grande"), così un host vede subito il problema.
- Uno strumento dell'host che lancia un'eccezione è un passo fallito. Il modello riceve "lo strumento non è riuscito" e il turno continua.

**Il nome dell'assistente**
- `Agent:Name` (default "Filum") dà il nome all'assistente nelle istruzioni di piattaforma ("You are <nome>, the person's own assistant…") e nell'agent loop.
- Il resto del testo di piattaforma resta uguale.

**Le regole** funzionano come adesso:
- prima lo strato di piattaforma;
- poi il prompt del pacchetto (spec 016);
- poi il nucleo della persona.

Oltre al prompt, ciò che non deve mai succedere l'host lo impone nel codice:
- esclude strumenti del motore con `ExcludedTools`, anche quelli che scrivono nella memoria;
- i suoi strumenti possono proporre invece di scrivere.

Il testo di piattaforma non impone nessuna lingua: dice all'agente di rispondere nella lingua del messaggio della persona, e il prompt di un pacchetto può aggiungere altro.

**Prima e dopo un turno**
- Un host può registrare un **cancello del turno**, chiamato prima del modello con la persona e la conversazione. Può rifiutare il turno con un suo messaggio e un suo stato HTTP (per esempio 402 quando il piano della persona non ha più messaggi). Allora il modello non viene chiamato, non si salva niente e non si spende niente.
- Un host può registrare un **osservatore del turno**, chiamato solo dopo un turno che ha risposto, con i consumi del turno (modello, token, costo). Un turno fallito non chiama nessun osservatore, quindi un host che conta i messaggi non ne conta mai uno fallito.
- I consumi per persona restano leggibili da codice (`UsageService`) per un mese o da una data, non solo attraverso il gruppo dei consumi.

**L'host di esempio** (`samples/Filum.SampleHost`, senza dominio) è una piccola app ASP.NET che:
- ha il suo context con `FilumModel`, e crea le sue tabelle all'avvio (un host vero tiene le migrazioni, come fa il prodotto ospitato);
- identifica la persona nel modo più semplice che non sia un vero login, segnato chiaramente come solo d'esempio;
- carica il pacchetto di esempio;
- dà un nome al suo assistente;
- registra uno strumento il cui risultato è in evidenza (la data e l'ora correnti, come testo per il modello e come valore JSON per l'app);
- monta i gruppi di endpoint sotto un prefisso scelto da lui.

**I test**
- I test delle librerie girano qui contro Postgres in un container: la CI di questo repository e la macchina di uno sviluppatore hanno bisogno di Docker per questi. I test del motore continuano a non avere bisogno di niente.
- Il prodotto ospitato tiene i suoi test d'integrazione.

## Verifica di piattaforma

1. **Generico:** sì. I punti di aggancio non conoscono nessun dominio, e lo strumento (l'ora) e il pacchetto (un diario) dell'host di esempio non ne nominano nessuno.
2. **Sensibilità e riservatezza:**
   - ogni query resta limitata all'id della persona, che dà solo l'host;
   - gli strumenti dell'host vedono solo la persona del loro turno;
   - la sensibilità la applica sempre il motore;
   - `data` viene mostrato solo all'app della persona;
   - nessun codice, regola o dato di un prodotto entra in questo repository.

## Criteri di accettazione

1. Dato questo repository, quando lo si compila e se ne eseguono i test (con Docker per i test delle librerie), allora `Filum.Agent`, `Filum.Agent.Http` e l'host di esempio compilano e i loro test passano. I test del motore passano ancora senza Docker.
2. Dato il prodotto ospitato, quando usa `Filum.Agent` e `Filum.Agent.Http` dal submodule, allora non ne ha nessuna copia, l'API della sua app resta invariata (stessi percorsi e payload), e i suoi test passano.
3. Dato l'host di esempio con il suo strumento, quando un turno lo chiama, allora i passi della risposta contengono quella chiamata con la sua descrizione e il suo `data`, e l'input del modello contiene il testo ma non `data`.
4. Dato uno strumento dell'host con lo stesso nome di uno strumento del motore, quando l'host parte, allora si ferma con un messaggio che nomina lo scontro.
5. Dato uno strumento dell'host che lancia un'eccezione, quando un turno lo chiama, allora il passo è fallito, il turno risponde comunque, e niente dei dettagli dell'eccezione arriva alla persona.
6. Dato `Agent:Name` impostato su un altro nome, quando si compongono le istruzioni di un turno, allora cominciano con "You are <nome>"; senza, cominciano con "You are Filum".
7. Data una richiesta a un gruppo montato senza persona, quando viene inviata, allora la risposta è 401. Date due persone, quando ognuna elenca le conversazioni e legge i messaggi attraverso i gruppi montati, allora nessuna vede quelli dell'altra.
8. Dato un cancello del turno che rifiuta, quando si manda un messaggio, allora tornano lo stato e il messaggio dell'host, il modello non viene chiamato e non si salvano né messaggi né consumi. Dato un turno che fallisce, allora non viene chiamato nessun osservatore.
9. Dato uno strumento dell'host con un valore in evidenza oltre i 4 KB, quando viene chiamato, allora il passo è fallito. Dato uno entro il limite, quando si rilegge la conversazione, allora il passo porta ancora il suo `data`.
10. Dato `Agent:AllowModelChoice` spento, quando una richiesta indica un altro modello, allora il turno usa il modello di default dell'host.
11. Dato lo snapshot del catalogo, quando si aggiunge l'hosting, allora lo snapshot resta invariato.

## Domande aperte

Nessuna.
