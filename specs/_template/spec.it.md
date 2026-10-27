# <Nome della feature>

> Stato: Bozza | Rivista | Implementata
> Area: <motore / storage locale / MCP / strumenti / SDLC>
> Autore/data: <chi>, <YYYY-MM-DD>

## Problema

<2–5 frasi: cosa non funziona oggi, per chi, e quali evidenze esistono (note di meeting, scenari che falliscono, log, misure). Enuncia il problema, non la soluzione.>

## Obiettivo

<Una frase: il risultato osservabile quando questa cosa è fatta.>

## Non-obiettivi

<Elenco puntato di ciò che è deliberatamente FUORI perimetro. Questa sezione previene lo scope creep: sii esplicito. Es. "nessun nuovo connettore", "nessuna modifica all'app mobile".>

## Comportamento attuale

<Come si comporta oggi il sistema nell'area interessata. Indica il codice (`file:riga`) invece di ridescriverlo; esplicita solo ciò che il codice non rende ovvio.>

## Comportamento desiderato

<Il nuovo comportamento come affermazioni osservabili: cosa vede un utente, un client o un componente a valle, non come ci arriva il codice. Copri anche i percorsi di errore: servizio giù, input malformato, modello che restituisce spazzatura.>

## Verifica di piattaforma

<Rispondi a entrambe, una riga ciascuna:
1. È un mattone generico, valido per qualunque persona e dominio? Se ha senso solo per un dominio, va in un pacchetto o nei dati della persona, non nel motore.
2. Rispetta i livelli di riservatezza in ogni strumento che tocca, lascia i file della persona leggibili e suoi, e non aggiunge segreti, dati personali o riferimenti privati a questo repository pubblico?>

## Criteri di accettazione

<Numerati, ciascuno verificabile in modo indipendente, idealmente ognuno legato a un test. Forma dato/quando/allora. Un criterio che nessuno può verificare è un desiderio, non un criterio.>

1. Dato <precondizione>, quando <azione>, allora <risultato osservabile>.
2. Dato <precondizione di errore>, quando <azione>, allora <gestione osservabile dell'errore>.
3. ...

## Domande aperte

<Decisioni che una persona deve prendere prima di poter scrivere plan.md. Svuota questa sezione (spostando le risposte nelle sezioni sopra) prima di iniziare il piano.>

- [ ] <domanda> → <risposta una volta decisa>
