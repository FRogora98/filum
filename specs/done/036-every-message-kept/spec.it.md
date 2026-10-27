# Ogni messaggio conservato, qualunque sia il modello

> Stato: Implementata (2026-10-06). Rivista (il proprietario, 2026-10-06, sulla proposta "un hook che registra ogni messaggio della persona, qualunque sia il modello": "vai prosegui", poi "andiamo avanti")
> Area: MCP (`filum-mcp`), eval
> Autore/data: federico rogora (bozza di Claude), 2026-10-06

## Problema

Con `filum-mcp`, quello che dice la persona entra in memoria solo quando il modello dell'host decide di conservarlo (`memory_log`, o un file). Su LongMemEval `oracle` (20 domande, Claude Code con Haiku 4.5 e una sessione di consolidamento dopo ogni sessione di storia), Filum ha risposto giusto a 9 su 19. In 8 dei 10 errori la risposta era "non ne ho traccia".

Una cartella di memoria conservata da una domanda mostra il perché:
- la persona ha detto "Premier Silver", poi mesi dopo "Premier Gold";
- il modello ha tenuto un solo file e ha sovrascritto Silver con Gold;
- ha registrato un solo evento, che non c'entrava;
- il consolidamento non aveva niente da cui recuperare.

Un prodotto ospitato non ha questo punto debole: ogni messaggio viene registrato come evento dal codice.

## Obiettivo

Con una riga di configurazione nell'host, ogni messaggio che la persona invia è nel registro degli eventi prima che il modello lo legga. Niente dipende dalla diligenza del modello, quindi il consolidamento e la ricerca possono recuperare quello che il modello ha perso.

## Fuori obiettivo

- **Niente registrazione delle risposte:** le risposte dell'host sono del modello, e alla memoria servono prima di tutto le parole della persona.
- **Nessun host oltre a quelli con gli hook:** Claude Code li ha; gli altri host tengono `memory_log`.
- **Nessun cambiamento agli strumenti.**

## Comportamento desiderato

1. **`filum-mcp log [--home <cartella>]`** legge lo stdin.
   - Se lo stdin è il JSON di un hook dell'host con un `prompt`, il testo è quel prompt; altrimenti è tutto l'input.
   - Il testo viene registrato come eventi `said` (fonte `host`), in parti di al massimo 8.000 caratteri tagliate a fine riga. Le parti hanno un solo id di conversazione, ricavato dal `session_id` dell'hook quando c'è.
   - Non scrive niente sullo stdout, perché l'output di un hook può arrivare al modello, ed esce sempre con 0: un errore è una riga sullo stderr, mai un messaggio bloccato.
   - La cartella è `--home`, altrimenti quella del server (`FILUM_HOME`, altrimenti quella predefinita). Viene bloccata a ogni chiamata, come fa il server, quindi i due possono girare insieme.
2. **Il README** riporta l'hook di Claude Code: `UserPromptSubmit` che lancia `filum-mcp log`, con la stessa cartella del server.
3. **Le eval:** `--hook` (LongMemEval con `--claude filum`, e il growth test `mcp`) avvia Claude Code con quell'hook (`--settings`), così il guadagno si misura.

## Verifica di piattaforma

1. **Generico:** sì. Registra le parole della persona, qualunque sia il loro dominio.
2. **Privacy:** è locale, scritto nella cartella della persona, e si dimentica come ogni evento.

## Criteri di accettazione

1. Dato il JSON di un hook sullo stdin, quando gira `filum-mcp log --home <cartella>`, allora il prompt è un evento `said` con fonte `host`, niente viene scritto sullo stdout, e il codice di uscita è 0.
2. Dato un testo oltre gli 8.000 caratteri, allora viene registrato in parti con un solo id di conversazione, e niente si perde.
3. Data una cartella in cui non si può scrivere, allora il codice di uscita è comunque 0 e il motivo è sullo stderr.
4. Dato `--hook`, allora Claude Code viene avviato con impostazioni il cui hook `UserPromptSubmit` lancia `filum-mcp log` sulla cartella di memoria della prova.

## Domande aperte

Nessuna.

## Risultato (2026-10-06)

LongMemEval `oracle`, le stesse 20 domande, Claude Code con Haiku 4.5 tramite `filum-mcp`, con una sessione di consolidamento dopo ogni sessione di storia:

| | Senza l'hook | Con l'hook |
|---|---|---|
| Accuratezza | 9 su 19 (47%; la 20ª non è stata giocata) | **14 su 20 (70%)** |
| Estrazione | 2 su 4 | 4 su 4 |
| Più sessioni | 1 su 4 | 3 su 4 |
| Ragionamento temporale | 1 su 3 | 3 su 4 |
| Aggiornamenti di conoscenza | 1 su 4 | 0 su 4 |
| Astensione | 4 su 4 | 4 su 4 |

- Senza l'hook, 8 dei 10 errori erano "nessuna traccia": il modello non aveva conservato quello che era stato detto.
- Con l'hook tutto quello che è stato detto è nel registro. Restano gli aggiornamenti di conoscenza, dove il modello risponde da un file sovrascritto (spec 037).
- 96 sessioni dell'agente per giro, sull'abbonamento del proprietario; il giudice è costato meno di 0,01 $.
- Il benchmark si ripete dopo il refactoring del proprietario.
