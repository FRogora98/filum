# Standard del codice backend .NET

> Italiano. Il riferimento è la versione inglese, [`CODING-STANDARD.md`](CODING-STANDARD.md).

Versione: 1.0 (2026-10-07). Linee guida generali, indipendenti dal progetto.
Perimetro: servizi e librerie backend .NET (ASP.NET Core, minimal API, EF Core, Aspire). Il frontend e le app mobili
hanno standard a parte.
Nei nomi di esempio `Acme` sta per il prodotto e `Orders`/`Billing` per le aree funzionali.

## Come si usa

- Ogni regola ha un ID, un livello e un modo di verifica.
  - **MUST**: obbligatoria. Un'eccezione va scritta in `docs/EXCEPTIONS.md` con regola, file e motivo, e la
    approva il responsabile tecnico.
  - **SHOULD**: attesa. Una deviazione si accetta solo se rende il codice più semplice, e va spiegata nella PR o
    nel commit.
- Verifica: **[auto]** = controllata da build, analyzer o test di architettura in CI; **[review]** = controllata
  nell'audit.
- La ristrutturazione procede a iterazioni:
  1. **Audit**: una tabella con una riga per regola (rispettata / violata, file:riga, numero di violazioni).
  2. **Ristrutturazione** delle violazioni, una zona alla volta. Il comportamento non cambia e i test restano
     verdi a ogni commit. Ogni commit cita gli ID delle regole.
  3. **Nuovo audit.**
  - L'iterazione si chiude quando tutte le MUST sono rispettate e la CI è verde con tutti i controlli [auto].
- Questo documento vince su abitudini, documenti precedenti e stile del codice esistente. Se una regola è
  sbagliata, si cambia qui, non si ignora nel codice.

## 1. Soluzione e progetti (SOL)

- **SOL-01 MUST** Nomi dei progetti `<Prodotto>.<Ruolo>` (es. `Acme.Orders.Api`, `Acme.Billing.Core`). Nomi delle
  cartelle uguali ai nomi dei progetti. [review]
- **SOL-02 MUST** Un solo progetto web per servizio deployabile; `ServiceDefaults` e `AppHost` (Aspire) per i
  servizi. [review]
- **SOL-03 MUST** Le dipendenze tra progetti vanno in una sola direzione: servizio → librerie → astrazioni. Una
  libreria non referenzia mai un servizio. [auto: test di architettura]
- **SOL-04 MUST** La soluzione è divisa in cartelle di soluzione `src/` e `tests/` (più `samples/` per le
  librerie). [review]
- **SOL-05 MUST** Per ogni progetto di produzione esiste `<Progetto>.UnitTests`; per ogni servizio anche
  `<Servizio>.IntegrationTests`. [review]

## 2. Architettura e dipendenze (ARC)

Clean architecture: le regole di business non dipendono da framework, database, HTTP o provider di modelli.

- **ARC-01 MUST** Ogni componente ha questi layer, con dipendenze solo verso l'interno:
  - `Domain`: entità, value object, regole di business, interfacce dei port. Nessuna dipendenza da EF Core,
    ASP.NET, SDK esterni.
  - `Application`: casi d'uso e servizi applicativi. Dipende solo da `Domain`.
  - `Infrastructure`: implementazioni dei port (persistenza, HTTP client, provider LLM, file system).
  - `Api` (solo nei servizi): endpoint e DTO. Chiama solo `Application`.
  [auto: test di architettura]
- **ARC-02 MUST** Nelle **librerie** i layer sono **progetti separati**
  (es. `Acme.Billing.Abstractions`, `Acme.Billing`, `Acme.Billing.Storage.Sql`). Nei **servizi** sono **cartelle** di un solo progetto, con la direzione imposta dai test di
  architettura. [review + auto]
- **ARC-03 MUST** L'infrastruttura si raggiunge solo attraverso interfacce definite nel layer interno (port and
  adapter). Nessun `new` di client esterni fuori da `Infrastructure` e dalla composizione. [review]
- **ARC-04 MUST** Un tipo per layer: entità di persistenza, modello di dominio e DTO sono tipi diversi, con
  mapping esplicito scritto a mano (niente AutoMapper). [review]
- **ARC-05 MUST** Niente stato per richiesta o per esecuzione in singleton. [review]

## 3. Composizione e Program.cs (HOST)

- **HOST-01 MUST** `Program.cs` ≤ 100 righe. Contiene solo la sequenza di avvio: `AddServiceDefaults`, chiamate
  `Add<Prodotto><Area>()`, `Use…`, `Map<Prodotto>Endpoints()`, `Run`. Nessuna registrazione singola, nessun
  endpoint, nessuna lambda di business. [auto: conteggio righe + review]
- **HOST-02 MUST** Le registrazioni stanno in `Extensions/<Area>Extensions.cs`, una classe `static` per area
  (`PersistenceExtensions`, `AuthenticationExtensions`, `ServiceExtensions`…). I metodi pubblici si chiamano
  `Add<Prodotto><Area>` e restituiscono `IServiceCollection` per poter essere concatenati. [review]
- **HOST-03 MUST** Le librerie espongono un solo punto d'ingresso DI pubblico (`services.AddAcmeBilling(…)`)
  con un builder o delle options per le varianti. Chi le usa non registra le classi interne a mano. [review]
- **HOST-04 SHOULD** Implementazioni alternative (null object, fake per lo sviluppo) scelte nella composizione in
  base alla configurazione, non con `if` sparsi nel codice. [review]

## 4. API (API)

- **API-01 MUST** Minimal API raggruppate per feature: `Api/<Area>/<Feature>/` con dentro
  `<Feature>Endpoints.cs` (gruppo **e** rotte) e `Dtos/`. Un nome di file dice cosa c'è dentro: niente
  `*Mapper.cs` che contiene rotte. [review]
- **API-02 MUST** Gli handler sono metodi `static` con nome (es. `CreateOrder`), non lambda inline, e restano
  sottili: leggono input e claim, chiamano **un** servizio applicativo, mappano il risultato. Nessuna logica di
  business, nessuna query, nessun try/catch. [review]
- **API-03 MUST** Un solo punto di ingresso `Map<Prodotto>Endpoints()` che chiama i `Map<Feature>Endpoints()`.
  [review]
- **API-04 MUST** Autorizzazione con policy nominate, definite in un solo posto e applicate al gruppo. [review]
- **API-05 MUST** Validazione dell'input con un filtro di endpoint generico, non a mano in ogni handler. [review]
- **API-06 MUST** I DTO sono `record` con membri `required`; i DTO non escono mai dal layer `Api`. [review]
- **API-07 SHOULD** Le rotte sono costanti, non stringhe ripetute. [review]

## 5. Servizi, classi e SOLID (SVC)

- **SVC-01 MUST** Una classe = una responsabilità che si descrive in una frase senza "e". Se servono due frasi,
  sono due classi. (SRP) [review]
- **SVC-02 MUST** File ≤ 300 righe, metodo ≤ 50 righe. **SHOULD** classe ≤ 200 righe, metodo ≤ 25 righe.
  In una codebase sana la mediana dei file sta intorno alle 30-40 righe. [auto: analyzer]
- **SVC-03 SHOULD** Al massimo 5 dipendenze nel costruttore; di più segnala una classe che fa troppo. [auto]
- **SVC-04 MUST** Il comportamento che varia (per tipo, provider, capitolo, canale) si estende aggiungendo una
  classe, non modificando un `if`/`switch` esistente: Strategy registrata con keyed services o con
  `IEnumerable<T>`. (OCP)
  [review]
- **SVC-05 MUST** Le sottoclassi rispettano il contratto della base; niente `NotImplementedException` o metodi
  vuoti per "accontentare" un'interfaccia. (LSP) [review]
- **SVC-06 MUST** Interfacce piccole e specifiche per chi le usa; nessuna interfaccia "di tutto" (una classe che implementa molte interfacce
  diverse va divisa). (ISP) [review]
- **SVC-07 MUST** Si dipende da astrazioni, iniettate dal costruttore (primary constructor). Niente service
  locator, niente `static` con stato, niente `DateTime.Now`: si usa `TimeProvider`. (DIP) [review]
- **SVC-08 MUST** Ogni servizio applicativo ha un'interfaccia. Se la cartella della feature ha al massimo 8 file,
  l'interfaccia sta accanto all'implementazione; oltre, le interfacce vanno nella sottocartella `Contracts/` (con il
  suo namespace). La scelta si fa per cartella, non per file. I port verso l'infrastruttura stanno comunque nel
  layer interno (ARC-03). [review]
- **SVC-09 MUST** Niente copie di servizi per versione (`ServiceV2`, `ServiceV3`): le varianti passano per
  Strategy o per mapper versionati. [review]

## 6. Dati (DATA)

- **DATA-01 MUST** EF Core: `DbContext` e configurazioni solo in `Infrastructure/Persistence`; una
  `IEntityTypeConfiguration<T>` per entità in `Configurations/`, applicate con `ApplyConfigurationsFromAssembly`.
  [review]
- **DATA-02 MUST** Niente repository generici sopra EF. I servizi applicativi usano porte specifiche
  (es. `IOrderStore`) implementate in `Infrastructure`; le query riusabili diventano Specification. [review]
- **DATA-03 MUST** Le letture usano `AsNoTracking()`. [review]
- **DATA-04 MUST** Nomi di tabelle e colonne come costanti, mai stringhe sparse. [review]

## 7. Configurazione (CFG)

- **CFG-01 MUST** Ogni sezione di configurazione è una classe `sealed` con suffisso `Options`, registrata con
  `AddOptionsWithValidateOnStart<T>().Bind(GetSection(T.SectionName))` e validata (`Validate` o DataAnnotations).
  Una configurazione sbagliata ferma l'avvio. [review]
- **CFG-02 MUST** Si inietta `IOptions<T>` (o `IOptionsMonitor<T>`), mai `IConfiguration`, fuori dalla
  composizione. [auto: test di architettura]
- **CFG-03 MUST** Nessun segreto nel codice, in `appsettings.json` o nei test. Segreti da Key Vault o variabili
  d'ambiente. [auto: secret scanning]

## 8. Errori (ERR)

- **ERR-01 MUST** I fallimenti attesi del business (non trovato, non permesso, limite superato) sono un
  `Result`/`Result<T>` restituito dal servizio, non eccezioni. [review]
- **ERR-02 MUST** Le eccezioni sono solo per errori inattesi; un unico gestore centrale (`IExceptionHandler`) le
  trasforma in `ProblemDetails`. [review]
- **ERR-03 MUST** Niente `catch (Exception)` che ingoia l'errore. Un catch esiste solo se aggiunge contesto o
  recupera davvero. [review]

## 9. Logging (LOG)

- **LOG-01 MUST** `ILogger<T>` con messaggi strutturati (template con segnaposto, non interpolazione). [auto]
- **LOG-02 SHOULD** Log frequenti o sul percorso caldo con `[LoggerMessage]`. [review]
- **LOG-03 MUST** Nessun dato personale, segreto o contenuto di conversazione nei log. [review]

## 10. Design pattern (PAT)

- **PAT-01 MUST** Un pattern si usa quando risolve un problema presente, non uno futuro. Ogni pattern ha un nome
  riconoscibile nel tipo (`…Strategy`, `…Factory`, `…Specification`, `…Handler`). [review]
- **PAT-02** Quelli previsti, con il caso d'uso:
  - **Strategy** per i comportamenti che variano (SVC-04);
  - **Factory** quando la creazione dipende da configurazione o ambiente;
  - **Specification** per query riusabili (DATA-02);
  - **Null object** per dipendenze opzionali;
  - **Result** per i fallimenti attesi (ERR-01);
  - **Template method** per famiglie di handler con lo stesso scheletro;
  - **Decorator** per comportamenti trasversali (retry, cache, misure) senza toccare la classe decorata.
- **PAT-03 MUST** Niente pattern usati a sproposito: interfacce con una sola implementazione che non serve mai
  sostituire nei test, factory che fanno solo `new`, livelli di indirezione senza motivo. [review]

## 11. Codice C# (CS)

- **CS-01 MUST** `<Nullable>enable</Nullable>` e `<ImplicitUsings>enable</ImplicitUsings>` in tutti i progetti,
  nessun warning di nullabilità soppresso senza motivo. [auto]
- **CS-02 MUST** Namespace a livello di file; namespace = percorso della cartella. [auto]
- **CS-03 MUST** Un tipo pubblico per file, file con lo stesso nome del tipo. [auto]
- **CS-04 MUST** Classi `sealed` per default; si toglie `sealed` solo per l'ereditarietà voluta. [auto]
- **CS-05 MUST** Ogni metodo asincrono riceve e inoltra un `CancellationToken ct`. [auto]
- **CS-06 MUST** Niente magic string o magic number: costanti in classi statiche annidate (`Constants.X.Y`).
  [review]
- **CS-07 SHOULD** `partial` solo per dividere un tipo grande per aspetto (`X.cs`, `X.Validation.cs`), mai per
  nascondere una classe che andrebbe divisa. [review]
- **CS-08 MUST** Nomi in inglese, che dicono cosa fa la cosa. Niente abbreviazioni, niente `Helper`/`Utils`/
  `Manager` generici. [review]
- **CS-09 MUST** Niente codice morto, codice commentato o `TODO` senza un riferimento a una voce di backlog.
  [auto: analyzer]

## 12. Commenti e documentazione (DOC)

- **DOC-01 MUST** Il codice si spiega da solo con nomi e struttura. Un commento in linea spiega **perché**, mai
  **cosa**. Niente commenti che ripetono il nome del metodo, niente storia delle modifiche nei commenti. [review]
- **DOC-02 MUST** XML `<summary>` solo su: API pubblica delle librerie (obbligatoria, una o due frasi), interfacce
  dei servizi, classi di options. Niente `<param>` vuoti. [auto per l'API pubblica: CS1591]
- **DOC-03 MUST** Documentazione del repo essenziale e senza duplicati, ogni fatto in un solo posto:
  - `README.md`: cos'è, come si avvia, link agli altri documenti;
  - `ARCHITECTURE.md`: progetti, layer, flussi principali;
  - `docs/`: solo flussi e decisioni che il codice non mostra.
  [review]
- **DOC-04 MUST** Un documento che non corrisponde più al codice si corregge o si cancella nello stesso commit
  che cambia il codice. [review]

## 13. Test (TEST)

- **TEST-01 MUST** La struttura delle cartelle dei test rispecchia quella del codice. [review]
- **TEST-02 MUST** Nomi `Metodo_QuandoCondizione_DeveRisultato` in inglese: `Method_WhenCondition_ShouldResult`.
  [review]
- **TEST-03 MUST** Classi di test `<ClasseTestata>Tests`. Test unitari senza I/O; test d'integrazione con
  `WebApplicationFactory` e Testcontainers condivisi con collection fixture. [review]
- **TEST-04 MUST** Fake e builder condivisi in un solo posto per progetto di test (`Fakes/`, `Builders/`), non
  duplicati per file. [review]
- **TEST-05 MUST** I test di architettura (NetArchTest o ArchUnitNET) verificano ARC-01, SOL-03, CFG-02 e girano in
  CI. [auto]
- **TEST-06 SHOULD** `TimeProvider` finto per il tempo, Bogus o builder per i dati. [review]

## 14. Build e qualità (BUILD)

- **BUILD-01 MUST** Gestione centralizzata dei pacchetti (`Directory.Packages.props`,
  `ManagePackageVersionsCentrally`) e `Directory.Build.props` comune (target framework, nullable, analyzer). [review]
- **BUILD-02 MUST** `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `.editorconfig` completo con
  le regole di questo documento che si possono automatizzare. [auto]
- **BUILD-03 MUST** La CI esegue build, test unitari, test d'integrazione e test di architettura a ogni push, e
  blocca il merge se falliscono. [auto]
- **BUILD-04 SHOULD** Copertura misurata (coverlet) e pubblicata in CI. [auto]

## 15. Librerie e SDK (SDK)

- **SDK-01 MUST** Tutto è `internal` per default; è `public` solo ciò che serve a chi usa la libreria. [review]
- **SDK-02 MUST** L'API pubblica è tracciata (`PublicApiAnalyzers`, file `PublicAPI.Shipped.txt`): ogni modifica
  all'API pubblica è visibile nel diff. [auto]
- **SDK-03 MUST** Un progetto `*.Abstractions` con le sole interfacce e i modelli che un host o un plugin deve
  conoscere (es. l'interfaccia dello storage); le implementazioni stanno in pacchetti separati. [review]
- **SDK-04 MUST** Versionamento semantico; una modifica incompatibile dell'API pubblica alza la major. [review]
- **SDK-05 MUST** Un progetto in `samples/` che mostra l'uso minimo in poche righe e compila in CI. [auto]
- **SDK-06 MUST** Nessuna dipendenza da un host specifico (ASP.NET, Aspire, una VM): la libreria si usa anche da una
  console. [auto: test di architettura]

## 16. Microservizi (MS)

Vale quando il sistema è fatto di più servizi deployati in modo indipendente. Le sezioni 1-15 descrivono l'interno di
ogni servizio; questa descrive come i servizi convivono.

- **MS-01 MUST** Ogni servizio possiede i suoi dati: un database (o schema) per servizio, nessun altro servizio lo
  legge o lo scrive direttamente. Si accede ai dati altrui solo attraverso la loro API o i loro eventi. [review]
- **MS-02 MUST** Un servizio corrisponde a un'area di business (bounded context), non a un layer tecnico. Niente
  servizi "di sole entità" o "di sole utility". [review]
- **MS-03 MUST** Nessuna libreria condivisa con modelli di dominio. Si condividono solo contratti (DTO, eventi) in
  pacchetti versionati e infrastruttura trasversale (`ServiceDefaults`, autenticazione). [auto: test di architettura]
- **MS-04 MUST** Contratti versionati: API con versione nella rotta o nell'header, eventi con versione nello schema.
  Una modifica incompatibile crea una nuova versione, la vecchia resta finché ha consumatori. [review]
- **MS-05 MUST** Ogni chiamata HTTP tra servizi passa per un client tipizzato con timeout, retry con backoff e
  circuit breaker (`Microsoft.Extensions.Http.Resilience`). Nessuna chiamata senza timeout. [review]
- **MS-06 SHOULD** Comunicazione asincrona (messaggi, eventi) quando chi chiama non ha bisogno della risposta subito;
  sincrona solo per le letture che servono per rispondere. [review]
- **MS-07 MUST** Gli handler dei messaggi sono idempotenti: lo stesso messaggio ricevuto due volte non produce effetti
  doppi. [review]
- **MS-08 MUST** Un cambio di stato e la pubblicazione del relativo evento avvengono insieme (pattern outbox), mai
  "salvo e poi pubblico" in due passi indipendenti. [review]
- **MS-09 MUST** Osservabilità comune a tutti i servizi tramite `ServiceDefaults`: OpenTelemetry per tracce, metriche
  e log, correlation id propagato in HTTP e messaggi, health check separati di liveness e readiness. [review]
- **MS-10 MUST** Autenticazione anche tra servizi (token di servizio o identità gestita); nessuna fiducia basata solo
  sulla rete interna. [review]
- **MS-11 MUST** Ogni servizio si builda, si testa e si deploya da solo: pipeline propria, nessun deploy coordinato
  obbligatorio. [review]
- **MS-12 SHOULD** Test di contratto tra produttore e consumatore (es. Pact) per le API e gli eventi condivisi.
  [auto]
- **MS-13 MUST** Nessuna transazione distribuita. I processi che toccano più servizi usano una saga (orchestrata o a
  coreografia) con azioni di compensazione esplicite. [review]

## 17. Scelte di base e motivazioni

- **Minimal API** invece dei controller: meno cerimonia, gruppi per feature, filtri di endpoint componibili.
- **Layer come progetti** nelle librerie (il compilatore impone i confini e l'API pubblica è controllata), **come
  cartelle** nei servizi (meno progetti da gestire, confini imposti dai test di architettura).
- **Result per i fallimenti attesi** invece di eccezioni: il flusso è esplicito nella firma e le eccezioni restano per
  i casi davvero imprevisti.
- **Interfacce accanto alle implementazioni** nelle cartelle piccole, in `Contracts/` in quelle grandi (SVC-08).

Da evitare anche se diffuso in molte codebase: segreti nel codice, file da migliaia di righe, copie di servizi per
versione, namespace a blocco, assenza di analyzer.
