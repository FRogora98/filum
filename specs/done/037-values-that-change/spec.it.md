# Valori che cambiano: il vecchio resta in vista

> Stato: Implementata (2026-10-06). Rivista (il proprietario, 2026-10-06, sulla proposta "i file ricordano cosa dicevano prima, e una sola ricerca tra file e quello che è stato detto": "vai procedi")
> Area: motore (strumenti `memory_read`, `memory_search`)
> Autore/data: federico rogora (bozza di Claude), 2026-10-06

## Problema

Con ogni messaggio conservato (spec 036), Haiku tramite `filum-mcp` ha risposto giusto a 14 domande su 20 su LongMemEval `oracle`. Sugli aggiornamenti di conoscenza ha risposto 0 su 4, e Gemma sul servizio ospitato 2 su 4. La memoria contiene i fatti; il modello risponde dal posto sbagliato:
- **Valori sovrascritti:** il modello modifica un file sul posto ("Premier Silver" diventa "Premier Gold") invece di usare `fact_record`, quindi il file mostra solo il valore nuovo. Alla domanda "com'era prima?" il modello legge il file e non trova niente. Il vecchio valore sta nelle revisioni del file, dove non guarda mai.
- **Valori vecchi o in conflitto:** il modello legge una fonte e si ferma. Ha risposto "30 dozzine di uova" da un file, quando quello che è stato detto dopo dice 20. La ricerca copre solo i file, quindi il messaggio successivo non compare mai accanto al file.

Le istruzioni che chiedono al modello di controllare il registro sono state provate (spec 031) e non bastano per i modelli piccoli.

## Obiettivo

È il motore a mettere davanti al modello il vecchio valore e l'ultima parola, qualunque cosa il modello scelga di fare:
- la lettura di un file mostra cosa dicevano prima le sue righe cambiate;
- una ricerca mostra, accanto ai file, i messaggi datati che corrispondono.

## Fuori obiettivo

- **Nessuno strumento nuovo,** e nessun cambiamento a come si scrivono file e fatti.
- **Nessun fatto automatico ricavato dai file:** quello che è cambiato si mostra, non si reinterpreta.

## Comportamento desiderato

1. **`memory_read`: il vecchio valore accanto al nuovo.**
   - Una riga del file che ne ha sostituita un'altra in una delle sue ultime 20 revisioni porta una nota dopo di sé: quando è cambiata, e la parte della vecchia riga che differisce (`(changed 2023-05-30; before: "…Premier Silver…")`).
   - Al massimo 30 note per lettura, e al massimo 200 caratteri ciascuna.
   - Le righe mai cambiate si leggono come prima.
2. **`memory_search`: file e quello che è stato detto, insieme.**
   - Dopo le righe dei file che corrispondono, elenca fino a 5 messaggi del registro degli eventi che corrispondono, dal più recente, ciascuno con la sua data e chi l'ha detto.
   - Un passaggio di consolidamento non riceve questa sezione, perché legge già gli eventi.
   - I messaggi privati compaiono solo con `includePrivate`, come per i file.

## Verifica di piattaforma

1. **Generico:** sì. Qualunque riga di qualunque file, qualunque messaggio.
2. **Sensibilità:** la ricerca segue per i messaggi la stessa regola dei file. Una nota mostra solo il contenuto passato del file stesso, a chi può leggere il file.

## Criteri di accettazione

1. Dato un documento la cui riga è stata modificata da "Premier Silver" a "Premier Gold", quando viene letto, allora la riga mostra "Premier Gold" con una nota che prima era "Premier Silver", e quando.
2. Data una collezione la cui riga è stata aggiornata (20 invece di 30), quando viene letta, allora la riga aggiornata porta il vecchio valore.
3. Dato un file mai cambiato dopo essere stato scritto, quando viene letto, allora non compare nessuna nota.
4. Data una riga di un file e un messaggio successivo che corrispondono entrambi, quando si cerca nella memoria, allora compaiono sia la riga del file sia il messaggio datato, prima il messaggio più recente; i messaggi privati solo con `includePrivate`.
5. Il catalogo degli strumenti resta entro il suo limite di dimensione.

## Domande aperte

Nessuna.

## Risultato (2026-10-06)

Le 4 domande sugli aggiornamenti di conoscenza dello stesso sottoinsieme, Claude Code con Haiku 4.5 tramite `filum-mcp`, con l'hook (spec 036) e il consolidamento: **0 su 4 prima di questa spec, 2 su 4 con essa.**

- Corrette: "lo status precedente", dove la lettura mostrava Silver accanto a Gold con la sua data; e "dove tenevo all'inizio", trovata tramite la ricerca.
- Ancora sbagliate:
  - un conteggio, che il modello ha preso dalla propria collezione invece che da quello che è stato detto;
  - una domanda a cui ha risposto senza cercare affatto.
- Le 20 domande complete si ripetono dopo il refactoring del proprietario.
- I test coprono i criteri da 1 a 5 (`ValuesThatChangeTests`, lo snapshot del catalogo e il suo limite di dimensione), e i test di integrazione del servizio ospitato sono stati aggiornati per le nuove letture e ricerche.
