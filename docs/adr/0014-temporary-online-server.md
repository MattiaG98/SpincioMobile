# ADR 0014 — Server online temporaneo (GitHub Codespaces) e indirizzo nel link di invito

- **Stato:** Accettato (13/10/2026, versione 1.6.0). Richiesta del proprietario: "opzioni furbe gratuite, temporanee,
  senza dover deployare l'app".
- **Integra:** ADR 0010 (il server resta lo stesso, con le stanze in memoria).

## Contesto
Il server online esiste dalla M7, ma non è pubblicato. L'indirizzo del server era scritto nell'app al momento del
build (`ServerUrl`), quindi ogni server nuovo richiedeva di ripubblicare l'app. Opzioni gratuite considerate (ottobre 2026):

| Opzione | Pro | Contro |
|---|---|---|
| **GitHub Codespaces** | Gratis con l'account GitHub che c'è già (circa 60 ore al mese su 2 core); niente da installare; si accende quando serve | Indirizzo diverso per ogni codespace; si spegne dopo 30 minuti senza attività (regolabile fino a 4 ore) |
| Cloudflare Quick Tunnel | Gratis, senza account | Serve un computer acceso con .NET; indirizzo casuale; "solo per test" |
| Render (piano gratuito) | Indirizzo fisso, si aggiorna da solo dal repo | Si addormenta dopo 15 minuti, risveglio di circa un minuto; account e configurazione da fare |
| Peer-to-peer (WebRTC) | Nessun server | Riscrittura della parte online; su rete mobile spesso serve comunque un relay (TURN) |

## Decisione
1. **L'app prende l'indirizzo del server dal link di invito** (`online?server=https://…&room=ABCDE`), oppure da un campo
   nella pagina Online. Lo ricorda sul dispositivo (`localStorage["spincio.server"]`). Accetta solo https (http solo per
   `localhost`). `ServerUrl` in appsettings resta come valore predefinito.
2. Nella stanza c'è **"Invita gli amici"**: sul telefono apre la condivisione (WhatsApp, Messaggi…); altrove copia il link.
   Il pulsante "Gioca online con gli amici" è sempre nel menu.
3. **Server temporaneo su GitHub Codespaces**:
   - `.devcontainer` installa .NET 10;
   - all'apertura `tools/online/start-server.sh` avvia il server, rende pubblica la porta 5080 e stampa il link da aprire
     sul telefono.
4. Mentre qualcuno è collegato, il server scrive una riga ogni 5 minuti (`ActivityHeartbeat`). In un codespace l'output
   del terminale conta come attività: resta acceso durante la partita e si spegne da solo dopo, senza consumare ore.
5. Un server fisso (per esempio Render) resta possibile in futuro senza toccare l'app: basta mandare il suo indirizzo
   nei link di invito o metterlo in `ServerUrl`.

## Conseguenze
- (+) Costo zero, nessuna ripubblicazione dell'app per cambiare server, invito con un tocco.
- (+) La stessa app funziona con il server in locale, in un codespace o su un hosting fisso.
- (−) Chi ospita deve aprire il codespace prima di giocare: la prima volta ci vogliono 2–3 minuti.
- (−) Un link che punta a un server sconosciuto fa collegare l'app a quel server. Il rischio è basso: il server riceve
  solo nome e mosse, e l'app accetta solo https.
- (−) Non verificato dentro un codespace vero, perché l'ambiente di sviluppo non può crearne uno. In locale script,
  server e link di invito sono provati (`tools/browser-checks/invite.mjs`).
