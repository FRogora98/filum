# Il benchmark: LongMemEval su Filum

> Stato: Implementata
> Area: eval / paper
> Autore/data: federico rogora (redatta da Claude), 2026-10-01
> Roadmap: 020, la prima spec del binario "benchmark e paper" della fase E

## Problema

La tesi del paper è che una memoria fatta di file con revisioni, più un controllo sulle dichiarazioni, rende un modello piccolo affidabile quanto uno grande nel ricordare, aggiornare e non inventare, a una frazione del costo. Oggi niente può sostenere questa tesi:
- **I nostri 17 scenari sono nostri:** brevi, sintetici, scritti da chi li mette alla prova. Un revisore chiederà, giustamente, un benchmark che il settore usa già.
- **Il growth test finora è un'esecuzione sola**, con 2 scenari e un modello: un aneddoto, non un risultato.
- **Niente confronta Filum con le alternative ovvie:** nessuna memoria, oppure recuperare le conversazioni passate in modo ingenuo.

## Obiettivo

Il runner gioca un sottoinsieme stratificato di LongMemEval su qualunque host del motore:
- con Filum e con i due riferimenti;
- con e senza il controllo delle dichiarazioni, e con e senza le revisioni;
- su almeno cinque modelli, dal piccolo al grande, con ripetizioni;
- entro un tetto di spesa.

Scrive report i cui numeri il paper usa così come sono.

## Non-obiettivi

- **Nessun altro benchmark per ora:** LoCoMo viene dopo, in una sua spec (i suoi dati sono CC BY-NC).
- **Nessun cambiamento al motore, al turno o agli strumenti.** Il benchmark li misura così come sono; le modifiche arrivano in spec successive, misurate rispetto a questa.
- **Nessun dato del benchmark in questo repository.** LongMemEval è MIT (© 2024 Di Wu), ma i suoi file sono grandi, quindi uno script li scarica dalla release degli autori. Si committano solo il nostro codice, i prompt del giudice degli autori (con la loro nota di copyright), i nostri sottoinsiemi (gli id delle domande) e i nostri report.
- **Ancora nessun testo del paper:** spec 028.
- **Niente esecuzioni in CI:** il benchmark chiama modelli a pagamento, quindi si esegue a mano.

## Comportamento attuale

- Il runner (`src/Filum.Evals`, spec 018) gioca file di scenari contro un host e un growth test `mcp`. Non sa niente di LongMemEval.
- LongMemEval:
  - 500 domande su cinque abilità: estrazione di informazioni, ragionamento su più sessioni, aggiornamento delle conoscenze, ragionamento temporale e astensione;
  - tre varianti: `S` (circa 40 sessioni, circa 115.000 token di storia per domanda), `M` (circa 500 sessioni) e `oracle` (solo le sessioni con le prove);
  - ogni domanda ha una data, e ogni sessione ha la sua.

## Comportamento desiderato

**Il protocollo, per una domanda:**
1. Una persona sintetica nuova, sull'host in prova.
2. **Ingestione:** ogni sessione della storia, in ordine di data, è una chat nuova. Una sessione più lunga di un messaggio (8.000 caratteri) si manda in parti consecutive della stessa chat, "parte 1 di 2"… Il messaggio dà la data della sessione e la sua trascrizione, presentate allo stesso modo per ogni sistema e modello ("Ecco una conversazione che hai avuto con la persona il <data>…"). Il sistema fa quello che farebbe con quel messaggio; per Filum, tenere ciò che vale la pena ricordare.
3. **La domanda:** posta in una chat nuova, con la sua data, dopo tutte le sessioni.
4. **Il punteggio:** la risposta si giudica con i prompt del giudice di LongMemEval, uno per abilità. Le domande di astensione passano quando il sistema dice di non sapere.

**I sistemi:**
- **Filum**, come lo ospita l'host: memoria a file con revisioni, il controllo delle dichiarazioni e il secondo tentativo.
- **Ablation**:
  - Filum senza il controllo delle dichiarazioni (`Reliability:CheckModel` vuoto);
  - Filum senza revisioni: `memory_history` e `memory_undo` esclusi con `ExcludedTools`, così non si torna indietro.
- **Come si sceglie un sistema:** un sistema Filum è un host configurato per quel sistema, e l'esecuzione lo nomina (`--system filum`, `filum-no-check`, `filum-no-revisions`). L'host di esempio prende la configurazione dall'ambiente, senza pacchetto, così il prompt è quello del motore.
- **Riferimenti**, giocati dal runner stesso con gli stessi modelli, fuori da qualunque host, chiamando direttamente il fornitore del modello da un file dei modelli (la stessa forma `Models` e `Providers` della configurazione di un host, chiavi da `<FORNITORE>_API_KEY`):
  - *nessuna memoria*: solo la domanda;
  - *recupero ingenuo*: le sessioni della storia, le prime k per BM25 rispetto alla domanda, messe prima di essa.

**I modelli grandi attraverso Claude Code** (l'abbonamento del proprietario, non il credito delle API):
- *Claude con Filum*: il braccio `mcp` della 018 sul protocollo di LongMemEval. Ogni sessione è una sessione nuova di `claude -p` con solo `filum-mcp` montato, e la domanda arriva in un'altra.
- *Claude senza memoria*: solo la domanda.
- *Claude con tutta la storia nel prompt*: il riferimento a contesto lungo.
- Girano soprattutto su `oracle`, con poche domande di `S`, sotto `--max-sessions`, distribuiti in più giorni, così non si toccano mai i limiti dell'abbonamento. Il report scrive la versione del modello che dichiara Claude Code.
- Qui Filum è l'MCP dentro un altro agente, non il suo loop con il controllo delle dichiarazioni: il paper lo dice.

**Le esecuzioni:**
- **Sottoinsieme:** un sottoinsieme stratificato di `S`, lo stesso numero di domande per abilità, scelto una volta con un seme fisso. Gli id delle domande si committano (`evals/longmemeval/subset-<n>.json`). Si gioca anche `oracle`, per separare "l'ha salvato?" da "sapeva rispondere?".
- **Modelli:** almeno cinque, dal piccolo al grande, dal catalogo dell'host.
- **Ripetizioni:** tre per domanda e sistema. Il report dà la media e la dispersione.
- **Costo:**
  - stimato prima dell'esecuzione, per sistema e modello, dalla dimensione della storia;
  - un tetto ferma l'esecuzione, come oggi;
  - il report dà token, latenza e dollari per domanda e per risposta corretta.

**I risultati:** scritti come oggi (`report.md`, `results.json`). Le esecuzioni che usa il paper si committano in `evals/reports/` con il comando esatto, il commit e la data, così ogni numero si può rigenerare.

## Verifica di piattaforma

1. **Generico:** sì. Il benchmark è un lettore di un dataset pubblico più un protocollo; non nomina nessun dominio, e il motore resta invariato.
2. **Sensibilità e riservatezza:**
   - le persone di LongMemEval sono sintetiche;
   - ogni esecuzione usa account sintetici nuovi;
   - non si committa nessun contenuto del dataset, solo id delle domande e i nostri punteggi;
   - il giudice vede la domanda, la risposta di riferimento e la risposta del sistema.

## Criteri di accettazione

1. Dato lo script di download, quando gira, allora i file di LongMemEval vengono scaricati in una cartella locale ignorata da git e controllati con le dimensioni o gli hash pubblicati; niente di loro viene committato.
2. Dato il dataset, quando si costruisce il sottoinsieme con il seme fisso, allora ha lo stesso numero di domande per abilità, e costruirlo di nuovo dà gli stessi id.
3. Data una domanda, quando si gioca su Filum, allora ogni sessione è un turno di una chat nuova in ordine di data, la domanda arriva in una chat nuova con la sua data, e la risposta si giudica con il prompt della sua abilità.
4. Data la stessa domanda, quando si giocano i due riferimenti, allora usano lo stesso modello, la stessa presentazione della domanda e lo stesso giudice; il recupero ingenuo riceve le prime k sessioni per BM25.
5. Date le ablation, quando girano, allora "senza il controllo delle dichiarazioni" non fa nessuna chiamata di controllo, e "senza revisioni" non offre `memory_history` né `memory_undo`.
6. Data un'esecuzione, quando si scrive il report, allora dà per sistema × modello × abilità:
   - l'accuratezza (media e dispersione sulle ripetizioni);
   - la correttezza delle astensioni;
   - le modifiche dichiarate ma non fatte;
   - token, latenza, e dollari per domanda e per risposta corretta.
7. Data una stima sopra il tetto, quando l'esecuzione parte, allora rifiuta a meno che la si forzi; durante l'esecuzione si ferma al tetto e riporta ciò che ha completato.
8. Dati i test del runner, quando girano (nessun modello, nessuna chiave), allora il lettore, il sottoinsieme, il protocollo, i riferimenti e il punteggio si provano su un piccolo file sintetico nel formato di LongMemEval.

## Domande aperte

Nessuna. Risposte del proprietario del 2026-10-01:
- **Budget:** il credito disponibile è di circa $15 (OpenAI e OpenRouter). La prima esecuzione è piccola, circa $3, sotto un tetto di $4, e il runner può farla quando la parte gratuita è pronta ("se costa davvero solo 3 dollari"). L'esecuzione grande per il paper aspetta altro credito, da decidere più avanti.
- **Giudice:** `gpt-5.4-mini`, fisso, su OpenAI.
- **Modelli:** `gemma-4-31b`, `glm-5.3-flash`, `deepseek-v4-flash` via OpenRouter, nel loop di Filum. Quelli grandi attraverso Claude Code, gestito con attenzione ("gestito bene e senza consumarmi token infiniti"). `gpt-5.5` resta fuori per ora.

Le proposte da cui vengono queste risposte:

- **Il budget di un'esecuzione completa.** L'ingestione è di circa 40 turni per domanda su `S`. Cifre approssimative, prima di misurare:
  - con un modello piccolo, circa $0,03 a domanda;
  - con uno grande, $1–2 a domanda;
  - per 100 domande × 4 sistemi × 5 modelli × 3 ripetizioni: decine di dollari con i modelli piccoli, centinaia con quelli grandi.

  → proposta:
  - una prima esecuzione di 50 domande (10 per abilità), 1 ripetizione, su tutti e cinque i modelli, per misurare i costi veri;
  - poi il sottoinsieme completo sui modelli piccoli;
  - e solo `oracle` con meno domande su quelli grandi.
- **Il giudice.** Gli autori di LongMemEval hanno usato GPT-4o. → proposta: tenere il nostro `gpt-5.4-mini` fisso, per il costo, e giudicare un'esecuzione con entrambi per riportare quanto concordano.
- **I cinque modelli.** → proposta: `gemma-4-31b`, `glm-5.3-flash`, `deepseek-v4-flash` (piccoli), `gpt-5.4-mini` (intermedio) e `gpt-5.5` (grande).
