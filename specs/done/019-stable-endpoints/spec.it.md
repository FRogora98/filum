# Endpoint stabili per le app

> Stato: Implementata
> Area: hosting / HTTP
> Autore/data: federico rogora (redatta da Claude), 2026-10-01
> Roadmap: 019, sincronizzata con il primo prodotto che ospita il motore (le schermate "cosa sa di te il coach" e dei consumi della sua app sono il primo client esterno)

## Problema

Dalla spec 017 qualunque host monta i gruppi di endpoint del motore (conversazioni, consumi, memoria, skill, modelli), e due app li chiameranno: l'app del prodotto ospitato e l'app del primo host. Oggi quel contratto esiste solo come codice:
- **Niente dice su cosa un client può contare.** Nessuna versione, nessuna descrizione, nessun elenco di rotte, campi ed errori.
- **Niente impedisce a una modifica di rompere un client.** Un campo rinominato o una rotta spostata compilerebbero, passerebbero i test del motore e romperebbero un'app in produzione, in un repository che non è questo.
- **Chi sviluppa un'app legge il sorgente** per conoscere un payload, e un client mobile non si può generare.

## Obiettivo

I gruppi diventano un contratto versionato e documentato (v1):
- una descrizione OpenAPI che qualunque host può servire;
- uno snapshot committato, così una modifica al contratto si vede e si rivede come il catalogo degli strumenti;
- una regola su cosa una modifica può fare senza una versione nuova.

## Non-obiettivi

- **Nessun cambiamento alle rotte o ai payload di oggi:** la v1 *è* il contratto di oggi, descritto.
- **Nessun client generato:** un client si può generare dalla descrizione, ma qui non se ne tiene nessuno.
- **Niente paginazione, filtri o endpoint nuovi:** arrivano quando un'app ne avrà bisogno, come aggiunte.
- **Niente autenticazione nella descrizione oltre a "quella dell'host":** ogni host documenta il suo login.
- **Nessuna API pubblica ospitata:** il contratto è per gli host e le loro app.

## Comportamento attuale

- `FilumEndpoints.MapFilumConversations | Usage | Memory | Skills | Models` (`src/Filum.Agent.Http/FilumEndpoints.cs`) montano rotte minimal-API senza nomi, riepiloghi o risposte dichiarate. I record dei payload stanno in `Dtos.cs` e `ConversationDtos.cs`, e gli errori sono problem details con un `title`.
- Il prodotto ospitato li monta sotto `/api`; il primo host li monterà sotto `/api/v1`.
- Niente li descrive, e nessun test fallisce quando cambia una rotta o un campo.

## Comportamento desiderato

**Il contratto, v1.**
- Ogni rotta ha un nome stabile (per esempio `filum.conversations.send`), un riepilogo di una riga, un tag per gruppo, e ogni risposta che può dare (stato e tipo del payload).
- Una risposta di errore è documentata come `application/problem+json` con il suo `title`.
- **La descrizione va bene per i client generati**, perché il primo host genera da lì il client della sua app:
  - il nome della rotta è il suo `operationId`, unico e stabile;
  - ogni schema di payload ha un nome stabile (il nome del record);
  - gli insiemi fissi di valori sono stringhe;
  - un valore che è solo un giorno è `format: date`, un istante `format: date-time`.
- `FilumApi.Version` è `"1"`. Ogni risposta dei gruppi porta l'header `Filum-Api-Version: 1`, così un client può verificare con cosa sta parlando.

**La descrizione.**
- Un host che la vuole serve un documento OpenAPI dei gruppi con il supporto standard di ASP.NET Core. L'host di esempio lo serve su `/sample/openapi/v1.json`.
- Il documento contiene solo i gruppi del motore e ciò che aggiunge l'host; il motore non aggiunge nessuna rotta per servirlo.

**Lo snapshot.**
- `docs/api/openapi-v1.json` in questo repository è il documento dell'host di esempio.
- Un test lo rigenera e lo confronta: una differenza fa fallire il test, come per lo snapshot del catalogo degli strumenti. Una modifica voluta si scrive con `FILUM_UPDATE_SNAPSHOT=1` e si rivede nel diff.

**La regola per le modifiche**, scritta in `docs/api/README.md`:
- Dentro la v1, una modifica può solo aggiungere: un campo facoltativo, una rotta nuova, un nuovo parametro di query facoltativo, una nuova risposta che un client può trattare come errore.
- Rinominare o togliere un campo o una rotta, cambiare un tipo o rendere obbligatorio qualcosa richiede la v2. Allora i gruppi della v2 si montano accanto a quelli della v1 per tutto il tempo che serve a un host, e l'header dice quale.
- `docs/api/README.md` elenca anche, per gruppo, cosa ci fa di solito un client:
  - mostrare la memoria di una persona e la sua storia;
  - annullare una modifica;
  - accettare una proposta di skill;
  - mostrare i consumi del mese.

**Gli host.**
- L'app del prodotto ospitato continua a funzionare senza cambiamenti: stesse rotte e stessi payload, più l'header.
- Il primo host monta i gruppi sotto il suo prefisso, e la sua app può contare sullo snapshot della versione che monta.

## Verifica di piattaforma

1. **Generico:** sì. Il contratto descrive gruppi generici (memoria, skill, conversazioni, consumi, modelli) e non nomina nessun dominio; le rotte proprie di un host sono sue.
2. **Sensibilità e riservatezza:**
   - la descrizione non contiene dati, solo forme;
   - ogni rotta continua a servire solo la persona che indica l'host;
   - i file privati mantengono la loro sensibilità nei payload, come oggi.

## Criteri di accettazione

1. Dato l'host di esempio, quando si genera il suo documento OpenAPI, allora elenca ogni rotta dei cinque gruppi con un `operationId` unico, un riepilogo, un tag, e lo stato e il tipo di payload di ogni risposta, con ogni schema di payload chiamato come il suo record.
2. Dato `docs/api/openapi-v1.json`, quando girano i test, allora il documento generato è uguale; dato un campo rinominato in un payload, allora il test fallisce finché lo snapshot non viene rigenerato.
3. Data qualunque risposta di un gruppo montato (successo o errore), quando un client ne legge gli header, allora `Filum-Api-Version` vale `1`.
4. Dato il prodotto ospitato, quando girano i suoi test, allora ogni rotta e ogni payload restano invariati; l'unica differenza è il nuovo header.
5. Dato `docs/api/README.md`, quando uno sviluppatore lo legge, allora vi trova cosa garantisce la v1, cosa richiede la v2 e a cosa serve ogni gruppo.
6. Dato lo snapshot del catalogo, quando si aggiunge il contratto, allora lo snapshot resta invariato.

## Domande aperte

Nessuna.
