# Runbook

> Procedure operative, da copiare e incollare. Tutti i comandi partono dalla radice del repository.

## Sviluppo quotidiano

| Cosa | Comando |
|---|---|
| Build | `dotnet build Spincio.slnx` |
| Tutti i test | `dotnet test Spincio.slnx` |
| Solo i test di accettazione | `dotnet test Spincio.slnx --filter "FullyQualifiedName~AT_"` |
| Gioco offline in locale | `dotnet run --project src/Spincio.Client` → http://localhost:5058 |
| Rigiocare una distribuzione | aggiungere `?seed=N` all'URL (N è il "Partita n." in fondo alla pagina) |

## Bot e simulatore

- Confronto tra L2 con mondi diversi: `--x Pimc --y Pimc --worlds 12 --yworlds 8` (Y usa `--yworlds`).

| Cosa | Comando |
|---|---|
| Torneo L1 contro L0 | `dotnet run --project tools/Spincio.Simulator -c Release -- --x Greedy --y Random --matches 1000 --seed 1` |
| Torneo L2 contro L1 | `dotnet run --project tools/Spincio.Simulator -c Release -- --x Pimc --y Greedy --matches 1000 --seed 5000 --worlds 16` |
| Tempo per decisione | `dotnet run --project tools/Spincio.Simulator -c Release -- --timing Pimc --worlds 8` |

I lati si invertono a ogni partita; le partite girano in parallelo e il risultato non dipende dallo scheduling. Per tarare un parametro, usare un seed per la taratura e **un altro** per la conferma.

## Online in locale
1. Server: `dotnet run --project src/Spincio.Server` → http://localhost:5080 (`/health` risponde `ok`).
2. Client in Development: `dotnet run --project src/Spincio.Client` (legge `wwwroot/appsettings.Development.json`, che punta al server locale).
3. Aprire http://localhost:5058 in due browser o finestre anonime → "Gioca online con gli amici".

Configurazione del server in `src/Spincio.Server/appsettings.json`:
- `AllowedOrigins` — le origini del client (CORS);
- `Rooms` — `TurnTimeout`, `ReconnectGrace`, `BotDelay`, `BotLevel` (`Greedy` o `Pimc`), `IdleLifetime`.

## Verifiche nel browser (Playwright)
- **Offline:** `dotnet publish src/Spincio.Client -c Release -o /tmp/pub`, poi `python3 -m http.server 8765` dentro `/tmp/pub/wwwroot`, poi `node tools/browser-checks/offline.mjs /tmp/out`. Stampa i tempi delle mosse della CPU ed eventuali errori in console.
- **Online:** avviare server e client come sopra, poi `node tools/browser-checks/online.mjs /tmp/out`.
- **Animazioni:** con il sito pubblicato su :8765, `node tools/browser-checks/animations.mjs` gioca 30 turni e controlla che nel mio turno nessuna carta sia ancora in volo o nascosta.
- **Punti:** con il sito pubblicato su :8765, `node tools/browser-checks/points.mjs /tmp/out` gioca una smazzata e controlla che ogni "+N" arrivi prima che il punteggio cambi e che, chiuso il riepilogo, la barra mostri il punteggio della partita; salva uno screenshot per tipo (spazzino, accuso, fine smazzata).
- **Tempo di decisione delle CPU nel browser:** con il sito pubblicato su :8765, `node tools/browser-checks/cpu-time.mjs` gioca a "Normale" e a "Difficile" con le animazioni spente e stampa il tempo di decisione per mossa (mediana, p90, massimo), tolta la pausa fissa di 450 ms.
- **Aggiornamento dell'app installata:** `dotnet publish src/Spincio.Client -c Release -o /tmp/pubA`, poi `dotnet publish src/Spincio.Client -c Release -o /tmp/pubB -p:Version=9.9.9`, poi `node tools/browser-checks/update.mjs /tmp/pubA/wwwroot /tmp/pubB/wwwroot 9.9.9 /tmp/out`: installa A, pubblica B, controlla che compaia la barra "Aggiorna" e che dopo il tocco l'app mostri v9.9.9. Usa la porta 8766.
- **Impaginazione:** con il sito pubblicato su :8765, `node tools/browser-checks/layout.mjs /tmp/out` gioca su tre schermi (375×667, 390×844, 430×932) e controlla a ogni mossa che la pagina non scorra, che la tavola non cambi misura e che le carte restino dentro; poi riempie la tavola con 8, 12 e 16 carte. Esce con codice 1 se qualcosa non va.
- **Carte:** `node tools/card-slicer/slice.mjs assets-source/carte-piacentine-al-completo.jpg src/Spincio.Client/wwwroot/cards` ritaglia di nuovo le 40 carte (ADR 0011). La pagina `/mazzo` dell'app mostra tutto il mazzo per controllarlo.
- Per fermare i processi usare l'ID del task o la porta, **non** `pkill -f` con un pattern che compare nel comando stesso.

## Pubblicazione su GitHub Pages (gratuita, repo pubblico)
1. Una volta sola: repository → **Settings → Pages → Source: GitHub Actions**.
2. Merge su `main`: il workflow `.github/workflows/pages.yml` esegue i test, pubblica il client e fa il deploy su `https://<utente>.github.io/SpincioMobile/`.
3. Oppure a mano: tab **Actions** → "Deploy to GitHub Pages" → *Run workflow*.

Il `<base href>` viene calcolato a runtime (sotto-percorso su github.io, `/` in locale): **non** riscrivere `index.html` dopo il publish, altrimenti il service worker rifiuta i file e l'app non funziona più offline.

## Giocare online con gli amici (server temporaneo, ADR 0014)
Chi ospita la partita:
1. da un computer (o dal telefono in "versione desktop") apre `https://github.com/MattiaG98/SpincioMobile`, pulsante
   verde **Code**, scheda **Codespaces**, **Create codespace on main**. Le volte dopo basta riaprire lo stesso codespace
   da `https://github.com/codespaces`: è più veloce.
2. Aspetta che nel terminale compaia **"Server Spincio acceso"** con un link (la prima volta 2–3 minuti).
   - Se compare "ATTENZIONE: rendi pubblica la porta 5080": scheda **PORTE**, tasto destro sulla porta 5080,
     **Visibilità della porta**, **Pubblica**.
3. Apre quel link sul telefono e scrive il proprio nome.
   - Chi ha già aperto l'app con questo server compare in **Giocatori online**: basta **Invita**. A chi viene invitato
     compare una notifica in alto; può accettare e scegliere **Con** o **Contro** chi invita (ADR 0015).
   - Per gli altri: **Crea una stanza**, poi **Invita con un link** e manda il link (WhatsApp va bene). Chi lo apre
     scrive il nome e preme **Entra**; da quel momento compare anche nella lista.
4. Quando ci sono tutti: **Inizia la partita**. I posti vuoti li prende la CPU. Gli inviti arrivano solo a chi ha l'app
   aperta (niente notifiche push).
5. Finito di giocare si chiude la scheda del codespace: si spegne da solo dopo 30 minuti senza giocatori. Il tempo si
   cambia in GitHub, **Settings → Codespaces → Default idle timeout**, fino a 4 ore. Gratis circa 60 ore al mese.

All'avvio lo script aggiorna il codice del codespace (`git pull --ff-only`), così un codespace creato prima di una
nuova versione ha comunque il server giusto (per esempio la lista dei giocatori online della 1.7.0). Se il server era
già acceso da prima dell'aggiornamento: chiudere il codespace e riaprirlo.

Il link di invito ha la forma `…/online?server=https://<codespace>-5080.app.github.dev&room=ABCDE`. Funziona anche con
un server fisso: basta cambiare l'indirizzo. Le stanze vivono in memoria: se il server si spegne, le partite in corso
finiscono.

Alternative (ADR 0014):
- **computer di casa con .NET**: `bash tools/online/start-server.sh` e
  `cloudflared tunnel --url http://localhost:5080` (Cloudflare Quick Tunnel, senza account); nei link si mette
  l'indirizzo `https://….trycloudflare.com`;
- **hosting fisso**: aggiungere l'origine del client ad `AllowedOrigins` e mettere il suo indirizzo nei link o in
  `ServerUrl`.

Verifiche:
- lo script si prova anche in locale: `bash tools/online/start-server.sh` avvia il server su `http://localhost:5080`;
- con il client pubblicato, `node tools/browser-checks/invite.mjs /tmp/pub/wwwroot /tmp/out` prova l'invito completo.
  Due giocatori: uno crea la stanza, l'altro apre il link ed entra, poi parte la partita.
- `node tools/browser-checks/lobby.mjs /tmp/pub/wwwroot /tmp/out` prova la lista dei giocatori online con tre
  giocatori: Cleo rifiuta un invito; Bea lo riceve nel menu iniziale, sceglie "Contro" ed entra nell'altra squadra; poi
  la partita parte. Salva gli screenshot della lista, della notifica e della stanza.

## Verificare che una partita online non sia truccata
A inizio partita tutti vedono l'impegno (`impegno xxxxxxxx` in fondo alla pagina). A fine partita il server rivela seed, sale e log. Il client ricalcola lo SHA-256 e **rigioca l'intera partita** con il motore; se il punteggio finale coincide mostra "partita verificata ✓". Codice: `Contracts/Commitment.Verify`.

## Setup dell'ambiente cloud (Claude Code)
Nel setup script dell'ambiente:
```bash
#!/bin/bash
set -euo pipefail
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /root/.dotnet
ln -sf /root/.dotnet/dotnet /usr/local/bin/dotnet
dotnet --list-sdks
```
Variabili d'ambiente: `DOTNET_ROOT=/root/.dotnet`, `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `DOTNET_NOLOGO=1`.

## Checklist di fine milestone
- [ ] `dotnet build Spincio.slnx -c Release` e `dotnet test Spincio.slnx -c Release` verdi
- [ ] Mutation testing manuale sulle regole o sui bot toccati
- [ ] Verifica nel browser se è cambiata la UI
- [ ] Aggiornati `docs/STATUS.md`, `docs/KNOWLEDGE.md` (lezioni apprese), `docs/AT-MAP.md`, gli ADR
- [ ] Versione aumentata in `Directory.Build.props` e voce in `CHANGELOG.md` (ADR 0012); dopo il merge, tag `vX.Y.Z`
- [ ] Commit con messaggio in inglese, push, CI verde
