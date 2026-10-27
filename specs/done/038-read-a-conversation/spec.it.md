# Leggere una conversazione intera

> Stato: Implementata (2026-10-17). Rivista (il titolare, 2026-10-17, "ipotesi 2, leggere una conversazione intera, in ordine, entro un budget")
> Area: motore (strumenti `events_search`, nuovo `events_read`)
> Autore/data: federico rogora (scritta da Claude), 2026-10-17

## Problema

`events_search` mostra al massimo `EventsSearchMax` (8) messaggi, ciascuno come estratto di circa 1.000 caratteri
attorno alla prima parola della domanda, con la risposta successiva tagliata a 300. Quando la risposta sta altrove nella
stessa conversazione (un dettaglio qualche turno più in là, una risposta lunga dell'assistente), il modello non la vede.
Su un benchmark di cronologie di chat lunghe, un confronto che dà al modello cinque conversazioni intere ha risposto a
più domande su una singola conversazione, e il distacco era dove l'estratto si fermava.

## Obiettivo

Un modello che ha trovato la conversazione giusta può leggerla tutta, in ordine, entro un budget, e raggiungere il resto di una più lunga.

## Fuori scopo

- Nessun cambiamento a come `events_search` ordina o a quante corrispondenze mostra.
- Nessun ordinamento per conversazione e nessun cambiamento ai fatti.
- Nessun cambiamento a cosa si conserva.

## Comportamento desiderato

1. **`events_search` dà il numero di ogni corrispondenza** (`#123`) dopo la data, così si può leggere nella sua
   conversazione. Il resto dell'output non cambia.
2. **`events_read`, nuovo strumento.**
   - **Argomento:** `event`, il numero di un evento (da `events_search`).
   - **Cosa dà:** tutti i messaggi della conversazione di quell'evento (detti, risposti, importati), dal più vecchio, per
     intero: numero, data e ora, chi l'ha detto e il testo, sotto il titolo della conversazione quando c'è.
   - **Un evento senza conversazione** (un'importazione, un messaggio registrato fuori da una chat) dà i messaggi dello
     stesso giorno.
   - **Budget:** al massimo 20.000 caratteri di messaggi per chiamata; un messaggio non si taglia mai. Una conversazione
     che ci sta arriva intera; una più lunga arriva da al massimo metà budget prima dell'evento, e il risultato dice
     quanti messaggi ci sono prima e dopo, con un numero di evento per leggere ciascuna parte (il budget di dimensione del
     catalogo della spec 031 non lasciava spazio a un argomento per scorrere).
   - **Rifiuti e privacy:** un evento sconosciuto o privato risponde che non esiste, e i messaggi privati di una
     conversazione restano fuori. I messaggi privati si raggiungono solo con `events_search` e `includePrivate` (il
     budget di dimensione del catalogo della spec 031 non lasciava spazio a un altro argomento).
3. **Descrizione:** "Read the whole conversation of an event number from events_search, in order." La descrizione di `events_search` perde "in every conversation" (il budget di dimensione del catalogo della spec 031).

## Verifica di piattaforma

1. Generico: ciò che è stato detto, in qualsiasi dominio; nessuna parola di dominio.
2. Sensibilità: i messaggi privati non escono mai da `events_read`; solo gli eventi di una persona.

## Criteri di accettazione

Gli stessi 7 di `spec.md`, nello stesso ordine.
