# ADR 0016 — Server online fisso su Render (piano gratuito)

- **Stato:** Accettato (13/10/2026). Richiesta del proprietario: "procediamo con il server fisso su Render".
- **Integra:** ADR 0014. Il server temporaneo su Codespaces resta come alternativa; la sua decisione 5 prevedeva già un
  server fisso. ADR 0010: le stanze restano in memoria.

## Contesto
Con un codespace bisogna accendere il server prima di ogni serata, e l'indirizzo cambia con ogni codespace nuovo. Con
la lista dei giocatori online (ADR 0015) un indirizzo fisso conta ancora di più: chi ha l'app aperta è sempre sullo
stesso server e compare nella lista senza link.

Render, piano gratuito (ottobre 2026):
- 512 MB di memoria, una frazione di CPU, 750 ore al mese per account (bastano per un server sempre acceso);
- si spegne dopo 15 minuti senza traffico in arrivo. Contano anche i messaggi sui WebSocket già aperti, e il client
  SignalR ne manda uno ogni 15 secondi: finché qualcuno ha l'app aperta, il server resta acceso;
- si riaccende alla prima richiesta, in circa un minuto;
- niente runtime .NET nativo: si usa un'immagine Docker.

## Decisione
1. **Immagine Docker** del server: `src/Spincio.Server/Dockerfile`, costruita dalla radice del repository.
   - Contiene solo server, motore, bot e contratti (`.dockerignore`).
   - Ascolta sulla porta che Render passa in `PORT`.
   - Usa il GC "workstation", adatto a poca memoria.
2. **Blueprint** `render.yaml`: servizio web `spincio-server`, piano `free`, regione `frankfurt` (la più vicina
   all'Italia), controllo di salute su `/health`.
   - Render lo ridistribuisce a ogni push su `main` che tocca il server o le librerie che usa.
   - Le modifiche alla sola app non lo riavviano, quindi non interrompono le partite in corso.
3. Il workflow `Server image` costruisce l'immagine e la avvia come fa Render (porta in `PORT`). Controlla `/health`
   e che l'app di GitHub Pages possa collegarsi (CORS). È la verifica dell'immagine: l'ambiente di sviluppo cloud non
   può scaricare le immagini base di Microsoft.
4. Quando il proprietario ha creato il servizio, il suo indirizzo va in `ServerUrl` (`wwwroot/appsettings.json`). Da
   quel momento l'app usa il server fisso senza link.
   - Un server preso da un link di invito, o scritto a mano, ha ancora la precedenza.
   - "cambia" nella pagina Online torna al server fisso.
5. La pagina Online avvisa che un server spento può metterci fino a un minuto a rispondere.

## Alternative valutate (13/10/2026, su richiesta del proprietario: "scegli l'opzione migliore gratuita, anche
peer-to-peer in 4")
| Opzione | Esito | Perché |
|---|---|---|
| **Render free** | ✅ Scelta | Gratuito e documentato da Render; un file (`render.yaml`) e un clic; aggiornamento solo quando cambia il server. Difetto: risveglio lento (circa un minuto) |
| Koyeb free | ❌ | Risveglio in pochi secondi, ma da febbraio 2026 l'azienda è di Mistral AI: piano gratuito incerto per i nuovi iscritti e, secondo più fonti, carta di credito con blocco di 29 $. Inoltre si aggiorna a ogni push, anche per le modifiche alla sola app, e interromperebbe le partite |
| Oracle Cloud "Always Free" | ❌ | Macchina sempre accesa, ma da amministrare (sistema, certificati). Risorse gratuite dimezzate a giugno 2026; le macchine poco usate possono essere spente |
| Cloudflare Durable Objects | ❌ | Stanze persistenti e risveglio istantaneo, gratis. Però gira solo JavaScript: il server andrebbe riscritto e le regole esisterebbero due volte, in due linguaggi. Il motore C# non entra nei limiti di un Worker |
| **Peer-to-peer (WebRTC)** | ❌ per ora, piano C | Vedi sotto |

**Peer-to-peer in 4.** È fattibile così:
- il telefono di chi invita fa da "host": esegue il motore C# e le regole della stanza, che si possono spostare dal
  server in una libreria comune;
- gli altri tre si collegano a lui con WebRTC (canali dati, a stella);
- per presentarsi serve comunque un piccolo servizio di "segnalazione", per esempio un Worker Cloudflare gratuito;
- serve anche un relay TURN: su rete mobile circa il 20–25% delle connessioni non passa senza (NAT del gestore).
  Cloudflare TURN è gratis fino a 1000 GB al mese, e una partita usa pochi KB.

Lo scartiamo perché, per questo gioco, è peggiore del server:
- su iPhone una PWA in background viene sospesa: se chi ospita apre WhatsApp, la partita si ferma per tutti, e
  servono riconnessione e ripristino dello stato;
- chi ospita ha in memoria le carte di tutti (con gli strumenti del browser potrebbe vederle);
- la garanzia "partita verificata" (ADR 0004) non vale più, perché chi ospita conosce il seed;
- il lavoro stimato è di diverse settimane: WebRTC, segnalazione, TURN, riconnessioni, cambio di host.

Il traffico di un gioco a turni è minimo, quindi il P2P non fa risparmiare nulla di concreto. Si riapre questa ADR solo
se sparisce l'hosting gratuito per i container.

## Conseguenze
- (+) Niente da accendere: si apre l'app e si gioca. Indirizzo fisso, aggiornamento automatico dal repository, costo
  zero.
- (−) Chi arriva per primo dopo un periodo di silenzio aspetta fino a un minuto.
- (−) Un riavvio fa finire le partite in corso, perché le stanze sono in memoria. Succede con un nuovo deploy del
  server o con una manutenzione di Render. Rimedio proposto senza database: il server consegna ai giocatori un
  "punto di ripristino" cifrato (seed e mosse, ADR 0003), che solo lui sa leggere; dopo un riavvio ricostruisce la
  stanza dal primo che rientra. È la prossima milestone proposta.
- (−) Serve un account Render collegato a GitHub, da creare una volta sola (passi in `docs/RUNBOOK.md`).
- (−) Il piano gratuito può cambiare condizioni. Se succede: il codespace (ADR 0014) funziona ancora, e un altro
  hosting Docker usa la stessa immagine.
