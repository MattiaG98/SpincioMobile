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

## Conseguenze
- (+) Niente da accendere: si apre l'app e si gioca. Indirizzo fisso, aggiornamento automatico dal repository, costo
  zero.
- (−) Chi arriva per primo dopo un periodo di silenzio aspetta fino a un minuto.
- (−) Un riavvio fa finire le partite in corso, perché le stanze sono in memoria. Succede con un nuovo deploy del
  server o con una manutenzione di Render.
- (−) Serve un account Render collegato a GitHub, da creare una volta sola (passi in `docs/RUNBOOK.md`).
- (−) Il piano gratuito può cambiare condizioni. Se succede: il codespace (ADR 0014) funziona ancora, e un altro
  hosting Docker usa la stessa immagine.
