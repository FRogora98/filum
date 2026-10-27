# Pronto per chi viene da fuori

> Stato: Implementata (2026-10-18). Rivista (il titolare, 2026-10-18, passo C6 del piano; il DCO al posto del CLA)
> Area: documentazione, CI, termini di contribuzione
> Autore/data: federico rogora (scritta da Claude), 2026-10-18

## Problema

Il repository è scritto come pubblico, ma a chi arriva mancano diverse cose:
- **Risultati.** Il README dice cosa fa Filum, ma non come si è misurato contro gli altri, con quali limiti, né come si
  confronta con Mem0, Zep e Letta.
- **Termini di contribuzione.** I termini sono un CLA, e mancano i file che dicono come contribuire, come segnalare una
  vulnerabilità e come comportarsi.
- **Lo standard di codice** che ogni modifica deve seguire è in italiano.
- **La CI** gira solo su Linux, mentre `filum-mcp` esce per Windows, macOS e Linux.

## Obiettivo

Chi apre il repository da fuori trova:
- cos'è Filum, come iniziare, com'è fatto e come si è misurato, onestamente;
- come contribuire con il DCO, come segnalare una vulnerabilità, e il codice di condotta;
- lo standard di codice in inglese;
- una CI che verifica ogni modifica sui tre sistemi per cui Filum esce.

## Fuori scopo

- Nessun cambio di codice nel motore né in `filum-mcp`.
- Nessuna release e nessuna pubblicazione del repository: sono del titolare.

## Comportamento desiderato

1. **README:**
   - "Results, with their limits": i turni pre-registrati come sono andati, e i loro limiti detti chiaramente;
   - "How it compares": Mem0, Zep/Graphiti, Letta, solo su ciò che dicono le loro fonti;
   - "How it is built": un breve riassunto, con il rimando ad `ARCHITECTURE.md`;
   - un comando per il benchmark pubblico (spec 043).
2. **Termini di contribuzione:**
   - `CONTRIBUTING.md`: la firma DCO, il flusso guidato dalle spec, i comandi, e cosa non entra mai;
   - `DCO` contiene il Developer Certificate of Origin 1.1, alla lettera;
   - `CLA.md` si toglie.
3. **`SECURITY.md`:**
   - segnalazione privata tramite gli advisory di sicurezza di GitHub;
   - cosa Filum raggiunge e cosa no (file, rete, sandbox);
   - cosa rientra nello scopo.
4. **`CODE_OF_CONDUCT.md`:** il Contributor Covenant 2.1, per riferimento, con come segnalare.
5. **Lo standard di codice:**
   - `docs/CODING-STANDARD.md` è in inglese: ogni regola, ID, livello e verifica restano;
   - l'originale italiano è il suo gemello, `docs/CODING-STANDARD.it.md`.
6. **CI:**
   - build e test su `ubuntu-latest`, `windows-latest` e `macos-latest`, a ogni push su `main` e a ogni pull request;
   - la scansione dei segreti e i test dell'harness di benchmark su Linux;
   - un controllo DCO che fa fallire una pull request con un commit senza firma.
7. **`CHANGELOG.md`** elenca la modifica per chi contribuisce.

## Verifica sulla piattaforma

1. Generico: solo documentazione e CI.
2. Nessun segreto, dato personale o riferimento privato. I risultati citano benchmark pubblici, e il confronto cita
   fonti pubbliche.

## Criteri di accettazione

1. Il README ha le quattro sezioni del punto 1. Ogni numero in "Results" è uno degli esiti pre-registrati. Ogni limite
   che l'audit del motore ha elencato per la valutazione è dichiarato.
2. `CLA.md` non c'è più; `DCO`, `CONTRIBUTING.md`, `SECURITY.md` e `CODE_OF_CONDUCT.md` ci sono, e la riga della
   licenza nel README rimanda al DCO.
3. `docs/CODING-STANDARD.md` è in inglese con gli stessi 96 ID di regola, nello stesso ordine, di
   `docs/CODING-STANDARD.it.md`.
4. I test del motore passano su Linux, lanciati in un container Linux prima di pubblicare il cambio della CI. Windows è
   questa macchina.
5. Il workflow di CI ha la matrice dei tre sistemi e il job DCO. La sua prima run su `main` è verde, o i suoi errori
   si correggono prima del passo successivo.
