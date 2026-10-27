# Sa chi sei: una misura

> Stato: Implementata (2026-10-19; segue il turno di riferimento). Rivista (il titolare, 2026-10-19, il test di accettazione della Traccia A; domande
> congelate prima delle spec 046-049)
> Area: valutazione (`evals/bench`)
> Autore/data: federico rogora (scritta da Claude), 2026-10-19

## Problema

LongMemEval e FactConsolidation misurano risposte su storie lunghe. Nessuno dei due misura ciò per cui esiste un agente
personale: che una sessione nuova, senza indizi, sappia chi è la persona, cosa vale adesso, cosa è ancora aperto, e
segua ciò che ha chiesto sessioni prima. E nessuno misura quanto costa la memoria a ogni turno.

## Obiettivo

Un comando misura, per una persona sintetica raccontata in otto sessioni, quanto sa e segue una sessione nuova, con e
senza gli hook di Filum, e quanto costa in contesto e tempo.

## Comportamento desiderato

1. **I dati** sono `evals/bench/data/knows-you.json`, congelati il 2026-10-19, prima delle spec 046-049. Contengono:
   - otto sessioni datate di ciò che dice la persona;
   - 20 domande con le risposte attese: identità, stato attuale, uno stato passato, una correzione, cose aperte,
     preferenze, astensione e una sintesi;
   - tre controlli di regola, richieste le cui risposte devono seguire ciò che la persona ha chiesto prima.
2. **`bench.py knows-you`** recita ogni sessione nella sua sessione, poi fa ogni domanda e ogni controllo in una
   sessione nuova. I bracci:
   - `filum-hooks`: filum-mcp, più il profilo all'avvio e il richiamo a ogni messaggio (gli hook di Claude Code, o lo
     stesso testo su un backend API);
   - `filum`: solo filum-mcp (le istruzioni dicono di chiamare prima `memory_overview`);
   - `none`: nessuna memoria, il minimo.
3. **Il giudice** chiede, per ogni domanda, se la risposta dà quella attesa. Per ogni controllo, se la risposta segue
   la regola.
4. **Il report** dà, per ogni braccio:
   - le domande giuste su 20, per tipo;
   - le regole seguite su 3;
   - i caratteri iniettati dagli hook (il profilo all'avvio, il richiamo per turno: media e massimo);
   - la latenza degli hook, p50 e p95;
   - il costo.

   I bracci si confrontano a coppie con il test esatto di McNemar (su così poche domande mostra solo differenze
   grandi).
5. **Backend,** come nella spec 043:
   - Claude Code, con gli hook scritti nelle impostazioni di progetto della sessione;
   - un'API compatibile con OpenAI, dove il profilo si aggiunge al prompt di sistema e ogni richiamo al suo messaggio,
     lo stesso testo che danno gli hook.

## Criteri di accettazione

1. Il file di dati si legge, ha 8 sessioni, 20 domande e 3 controlli, ed è committato prima delle spec 046-049.
2. Una run con un backend finto recita 8 sessioni per braccio e fa 23 richieste per braccio. Il suo report ha ogni
   campo del punto 4 (un test unitario).
3. Sul backend API, il prompt di sistema del braccio `filum-hooks` contiene l'uscita di `filum-mcp brief`, e il
   messaggio di ogni richiesta contiene il richiamo di `filum-mcp log` (un test con una build vera).
4. Un primo turno sul motore attuale (045) è il riferimento. Il turno della Traccia A si misura contro di esso, con le
   soglie fissate prima che giri.
