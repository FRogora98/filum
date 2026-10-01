# Memoria episodica: le conversazioni passate, consultabili

> Stato: Bozza
> Area: il turno / strumenti
> Autore/data: federico rogora (redatta da Claude), 2026-10-01
> Roadmap: fase E, binario del prodotto; chiesta dalla prima esecuzione di LongMemEval (spec 020)

## Problema

La prima esecuzione di LongMemEval (`oracle`, 20 domande, tre modelli piccoli) ha misurato cosa perde la memoria dell'agente:
- Filum ha risposto bene al 65–85%, mentre le sessioni grezze date agli stessi modelli hanno risposto bene al 95–100%.
- Le perdite stanno dove un riassunto lascia cadere i dettagli: il ragionamento temporale è al 50% per tutti e tre i modelli, e gli aggiornamenti al 50% con Gemma.

La ragione è strutturale. La memoria di Filum è ciò che l'agente ha scelto di scrivere, un riassunto, mentre le conversazioni stesse sono già salvate dal prodotto ospitato, con le loro date, e l'agente non può leggerle. Una persona che chiede "quando ho parlato per la prima volta della bici?" riceve ciò che il riassunto ha tenuto, non ciò che è stato detto.

## Obiettivo

L'agente ha due memorie:
- quella **curata**, i file: regole, preferenze, fatti tenuti aggiornati;
- quella **episodica**: ogni conversazione passata della persona, consultabile con la sua data.

Risponde da entrambe, e LongMemEval misura cosa cambia.

## Non-obiettivi

- **Nessuno storage nuovo:** le conversazioni sono già salvate dall'host (`conversations`, `conversation_messages`).
- **Ancora niente embedding:** la ricerca è lessicale (BM25, come il riferimento del benchmark). Il recupero semantico è la spec 023, misurato rispetto a questa.
- **Niente memoria episodica in `filum-mcp`:** lì l'agente dell'host tiene le sue trascrizioni, che Filum non vede. La sua parte arriva con l'ingestione (spec 021).
- **Nessun cambiamento al catalogo degli strumenti di memoria del motore.** Lo strumento nuovo appartiene al turno, che possiede le conversazioni.

## Comportamento attuale

- **Il turno** (`Filum.Agent/ConversationService`) salva ogni messaggio con la sua ora. L'agente riceve solo gli strumenti di memoria del motore.
- **`PlatformInstructions`** dice che la memoria sono i file e il nucleo.
- **LongMemEval, prima esecuzione** (`evals/reports/…`, spec 020):

| | Gemma | GLM | DeepSeek |
|---|---|---|---|
| Filum | 65% | 85% | 85% |
| sessioni grezze (BM25, k = 5) | 95% | 100% | 100% |

## Comportamento desiderato

**Lo strumento `conversation_search`:**
- **Parametri:** `query`, più le date facoltative `from` e `to`.
- **Cosa restituisce:** i messaggi passati della persona che corrispondono meglio (BM25 sui messaggi di tutte le conversazioni tranne quella corrente), fino a un limite. Ognuno arriva con la sua data, il titolo della sua conversazione e il messaggio che lo ha seguito (la risposta, o la replica della persona), così un fatto si legge nel suo contesto. Un messaggio lungo viene tagliato al suo inizio e alla parte intorno alla parola che corrisponde meglio.
- **Cosa cerca:** solo le conversazioni della persona, come ogni query del turno.
- **I risultati sono dati:** le regole di piattaforma lo dicono già per i file, e valgono anche qui.
- **Ogni chiamata è un passo** del turno, di tipo `searched`.

**Le istruzioni** guadagnano una regola. I file sono ciò che l'agente ha scelto di tenere; quando una domanda richiede un dettaglio, una data, o cosa è stato detto esattamente, cerca nelle conversazioni passate, e dice quando le due non sono d'accordo, vince la più recente.

**Configurazione:** `Agent:EpisodicMemory`, **spenta di default** finché il benchmark non mostra che aiuta e il proprietario non decide di accenderla; il comportamento del prodotto ospitato non cambia prima di allora. Spenta, lo strumento non viene offerto.

**Dove vive:** in `Filum.Agent`, come strumento che il turno aggiunge accanto a quelli del motore (come gli host aggiungono i loro). Il contratto dei gruppi di endpoint non cambia.

## Verifica di piattaforma

1. **Generico:** sì. Cercare nelle proprie conversazioni passate non nomina nessun dominio.
2. **Sensibilità e riservatezza:**
   - si cercano solo le conversazioni della persona;
   - una conversazione che la persona ha cancellato sparisce dalla ricerca, perché cancellare una conversazione ne cancella i messaggi;
   - i file privati della memoria non vengono toccati: questo strumento legge conversazioni, non file.

## Criteri di accettazione

1. Data una persona con conversazioni passate, quando l'agente chiama `conversation_search` con parole di un vecchio messaggio, allora riceve quel messaggio con la sua data, il titolo della sua conversazione e il messaggio successivo, e mai quelli di un'altra persona.
2. Dati `from` e `to`, quando cerca, allora tornano solo i messaggi in quella finestra.
3. Data la conversazione corrente, quando cerca, allora i messaggi della conversazione corrente non sono nei risultati.
4. Data una conversazione cancellata, quando cerca, allora non ne torna niente.
5. Con `Agent:EpisodicMemory` spenta, quando gira un turno, allora lo strumento non viene offerto.
6. Dato il sottoinsieme `oracle` da 20 di LongMemEval e i tre modelli piccoli, quando si gioca Filum con la memoria episodica, allora il report dà la sua accuratezza accanto a quelle di Filum e dei riferimenti della spec 020, per abilità.
7. Dato lo snapshot del catalogo del motore, quando si aggiunge lo strumento, allora resta invariato.

## Domande aperte

Nessuna.
