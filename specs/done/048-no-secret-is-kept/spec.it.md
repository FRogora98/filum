# Nessun segreto si tiene, e niente di passeggero

> Stato: Implementata (2026-10-19). Rivista (il titolare, 2026-10-19, Traccia A, un filtro dei
> segreti in ingresso e un filtro di durata su ciò che si salva)
> Area: motore (eventi, file, fatti, istruzioni), filum-mcp (istruzioni)
> Autore/data: federico rogora (scritta da Claude), 2026-10-19

## Problema

Le persone incollano chiavi, token e password nei loro agenti. Una memoria che tiene ogni messaggio li terrebbe per
sempre, in file in chiaro, nella loro storia. In una piccola memoria di agente personale, esaminata mentre si progettava
questa funzione, sono stati trovati un token e una password in chiaro. E un modello che salva tutto riempie la memoria di
ciò che alla sessione successiva non conterà.

## Obiettivo

Un segreto non arriva mai nella memoria, qualunque strada prenda. L'agente salva ciò che conterà ancora più avanti.

## Comportamento desiderato

1. **Ogni evento e ogni file si tiene con i segreti sostituiti** da `[secret removed by Filum]`. Vale per:
   - ciò che è stato detto, compreso l'hook `log`;
   - gli import;
   - ogni file scritto tramite il motore, compresa la tabella dei fatti.
2. **Cosa conta come segreto:**
   - i formati di chiave dei fornitori (`sk-…`, `ghp_…`, `github_pat_…`, `AKIA…`, `xox…-`, `AIza…`);
   - i JSON web token;
   - i blocchi di chiave privata;
   - un valore con etichetta (`password:`, `api_key=`, `client secret =`, …), dove l'etichetta resta e se ne va solo il
     valore.

   Il testo che parla soltanto di password o segreti resta com'è.
3. **La modifica a mano di un file è della persona,** e non viene cambiata.
4. **Spec 049, il filtro di durata.** Le istruzioni e il consolidamento salvano solo ciò che conterà ancora in una
   sessione successiva: non le chiacchiere, non i dettagli passeggeri del compito in corso.

## Criteri di accettazione

1. Ogni formato di chiave, un JSON web token, un blocco di chiave privata e una password con etichetta vengono tolti. Le
   frasi su password, segreti e token restano uguali (test unitari).
2. Una chiave detta in un messaggio, scritta in un file e registrata come fatto non è in nessuno dei tre (un test di
   contratto su ogni archivio).
3. Le istruzioni di entrambi i profili di filum-mcp contengono la regola di durata e restano sotto i 4.096 caratteri.
