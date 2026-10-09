# Base di conoscenza

> Cosa serve sapere per lavorare su Spincio senza ripetere errori già fatti.
> **Aggiungere una voce in "Lezioni apprese" ogni volta che un problema costa più di qualche minuto.**

## 1. Mappa della solution

```
Engine ◄── Bots ◄── Simulator (tools)
  ▲  ▲        ▲
  │  └── Contracts ◄──┐
  │         ▲         │
Client ─────┘      Server
(Blazor WASM)   (ASP.NET Core + SignalR)
```

| Progetto | Ruolo | Regola chiave |
|---|---|---|
| `Spincio.Engine` | Regole pure: `NewMatch`, `Apply`, `LegalCommands`, `ViewFor`, `Hypothetical` | Zero dipendenze, niente clock/RNG/I-O; tutta la casualità in `Pcg32` dentro lo stato |
| `Spincio.Bots` | L0 casuale, L1 greedy, L2 PIMC (entrambi i livelli dell'app, ADR 0013) | Ricevono solo `PlayerView` + `BotMemory`; solo `PimcBot` tocca stati **ipotetici** (ADR 0008) |
| `Spincio.Contracts` | DTO SignalR, `CommandCodec`, `SpincioJson`, `Commitment` | Condiviso da Client e Server; nessuna dipendenza da Bots/Client/Server |
| `Spincio.Client` | PWA Blazor WebAssembly | La UI vede solo `IGameSession` (vista del giocatore); mai `MatchState` |
| `Spincio.Server` | Stanze online autoritative | Una coda seriale per stanza; timer con `TimeProvider` |
| `tools/Spincio.Simulator` | Tornei tra bot, tempi di decisione | Contiene `MatchRunner` (usa `MatchState`, quindi non sta in Bots) |

Tutte queste regole sono **verificate da test** in `tests/Spincio.Architecture.Tests`.

## 2. Flussi principali

### Una mossa, offline
`Home.razor` → `IGameSession.PlayAsync(cmd)` → `LocalGameSession` → `SpincioEngine.Apply(state, cmd)` → nuovo stato + eventi → eventi filtrati per il giocatore (`EventsFor`) → **animazione** di ogni `CardPlayed` sul tavolo ancora vecchio (`IMoveAnimator`, `wwwroot/js/moves.js`) → solo allora lo stato viene accettato → feed, riepilogo, salvataggio (seed + log comandi) → `SettleAsync` toglie le carte volanti → loop delle CPU con pausa di 450 ms.

### Animazioni delle mosse (M10)
- Solo presentazione: il motore non sa nulla, una sessione senza `Animator` gioca identica (test `Without_an_animator_the_game_is_unchanged`).
- Le copie volanti stanno in un livello fisso (`#fly-layer`) sopra la pagina; gli originali vengono solo nascosti (`visibility`), mai spostati, così il DOM di Blazor resta coerente. `settle()` aspetta due frame dopo il nuovo render, poi svuota il livello e rimostra ciò che è ancora in pagina.
- Le carte sono trovate con `data-card` (notazione, es. `7D`); i posti con `.seat-1/2/3` e `.me`.
- Presa: vola (380 ms) → si posa sulla **prima** carta presa, non al centro del gruppo che potrebbe contenere carte non prese → evidenzia (260 ms) → raccoglie (220 ms) → porta al giocatore (420 ms). Calata: vola nel posto misurato con una carta-sonda invisibile.
- Durante l'animazione della propria carta la mano è disattivata (`LegalCommands` vuoto). Online gli aggiornamenti passano in fila (semaforo) e ognuno anima prima di mostrarsi.
- `prefers-reduced-motion`: nessuna animazione.
- **Punti** (1.1.0): `PointsGain` dice quali punti volano e da dove (spazzino → dalla tavola, accuso → dal posto di chi accusa, fine smazzata → dalla tavola); `spincioMoves.points()` mostra "+N" e lo porta sul punteggio ("Noi" o "Loro"), che si illumina. Spazzini e accusi passano da `AnimateMoveAsync` come le carte, quindi prima che lo stato cambi: il numero cambia solo all'arrivo. I punti di fine smazzata li anima `Home.razor` alla chiusura del riepilogo; finché il riepilogo è aperto la barra mostra `PointsGain.ScoreBefore` (punteggio partita − punti di fine smazzata).
- **Avatar** (1.5.0): `IGameSession.SeatAvatar` (offline `GameText.Avatar`: stessa posizione di `SeatName`, SVG originali in `wwwroot/avatars`; online `null` → iniziale del nome). `SeatView` aggiunge l'anello oro/rosso secondo la squadra. Il service worker mette in cache anche gli `.svg`.
- **Scritte** (1.3.0): `GameText.Banner` dà "MARIANA" (fante che prende 5 e 3, `CardPlayed.IsMariana()` nel motore, AT-40) oppure "Spazzino!". Una Mariana che non svuota la tavola è un `PointsGain` di tipo `Mariana` con 0 punti: in `moves.js` mostra solo la scritta e svanisce sul posto. A animazioni spente la scritta è la `.sweep-flash` della pagina (`BannerCount`/`BannerText` della sessione).
- Con le animazioni attive `moves.js` mette la classe `moves-on` su `<html>` e la scritta "Spazzino!" della pagina si nasconde (la mostra il pop dei punti); con le animazioni spente resta quella.
- Le carte volanti cambiano misura (left/top/width/height animati, poi fissati): partono grandi come la carta di origine (mano o dorso del posto) e arrivano grandi come le carte in tavola (carta-sonda o prima carta presa).

### Impaginazione senza scorrimento (1.0.0)
- `html, body` non scorrono; `.app` è alta `100dvh` e scorre solo se una pagina è più alta dello schermo (menu su schermi minuscoli, `/mazzo`, online). Lo schermo di gioco (`.app:has(> .board)`) non scorre mai.
- Il tabellone è una colonna flex: tutto ha altezza fissa tranne `.middle`, che prende lo spazio rimasto. Per questo hanno misure fisse anche la riga di stato (con il pulsante Accusa), la mano (anche vuota), i posti laterali (56 px) e il nome del mazziere; le dichiarazioni degli altri sono sovrapposte (`.seat-badges`).
- La tavola è un *container* CSS (`container-type: size`). `Home.razor` (`TableFit`) scrive in `--c1..--c4` quante colonne servono con 1–4 righe; per ogni numero di righe il CSS calcola la carta più grande che sta in larghezza e in altezza e prende la migliore, senza superare la misura delle carte in mano.

### Statistiche (1.2.0)
- `StatsTracker` (puro) aggiorna `PlayerStats` dagli eventi che il giocatore vede; `StatsService` le salva e segue la partita corrente tramite `IGameSession.LiveEvents`.
- `LiveEvents` parte solo per le mosse giocate dal vivo (dopo che la mossa è mostrata), **mai** quando una partita salvata viene rigiocata alla ripresa: niente doppi conteggi.
- Partita abbandonata = "Abbandona"/"Esci dalla stanza" prima della fine, oppure "Nuova partita" dal menu quando c'era una partita salvata. Azzera la serie di vittorie.
- Gli aggiornamenti sono in catena (ognuno parte dal risultato del precedente), anche mentre il primo caricamento da localStorage è in corso.

### Una mossa, online
Client: `RemoteGameSession.PlayAsync` → hub `Play(token, expectedSequence, "P0:7D>3S+4B")` → `GameRoom` (coda seriale) valida token, sequenza e regole → `AcceptAsync` → a ogni posto `GameUpdate(sequence, ViewFor(seat), EventsFor(seat))` → timer: pausa CPU oppure timeout di turno (30 s) → a fine partita `MatchReveal(seed, salt, log)` e il client verifica con `Commitment.Verify`.

### Decisione di L2 (PIMC)
Mosse legali dalla vista → se c'è un accuso lo dichiara → per N mondi: distribuisce a caso le carte non viste (coerenti con gli accusi della distribuzione) → `SpincioEngine.Hypothetical(view, guess)` → per ogni mossa candidata: rollout con L1 per tutti fino a fine smazzata → valore = differenza di punti (+ bonus tutti i denari) → media + `PriorWeight × L1.Evaluate` → mossa migliore.

## 3. Formati

| Cosa | Formato | Dove |
|---|---|---|
| Carta | `7D`, `KB` (rango `A,2..7,J,N,K` + seme `D,C,S,B`) | `Card.ToString/Parse` |
| Comando | `D0` accusa · `P1:7D` calata · `P1:7D>3S+4B` presa (carte prese in ordine canonico: seme, poi rango) | `Contracts/CommandCodec` |
| Salvataggio offline | JSON `{RulesVersion, Seed, Commands[], Difficulty}` in `localStorage["spincio.savedGame"]` | `SavedGame` |
| Seat online | `localStorage["spincio.onlineSeat"]` = token segreto del posto | `LocalStorageOnlineSeatStore` |
| Statistiche | JSON `PlayerStats` (con `Version`) in `localStorage["spincio.stats"]`; versione diversa o JSON illeggibile = statistiche vuote | `StatsService` |
| Eventi su SignalR | JSON con discriminatore `$kind`; `Seat` come numero | `SpincioJson` |
| Impegno del seed | `SHA-256("{seed}:{salt}")` in esadecimale minuscolo | `Commitment.Of` |

## 4. Numeri tarati (e perché)

| Parametro | Valore | Motivo |
|---|---|---|
| Pesi di L1 (`GreedyWeights`) | carta 1, denaro +1, sette +1,5, settebello +5, rebello +4, spazzino 10, rischio spazzino 8, costo calata 0,3 | Ablazione M2: ogni componente fa vincere; ×2 e ×0,5 sul rischio peggiorano |
| Mondi L2 | 16 nel simulatore/server; nel browser **4 ("Normale") e 12 ("Difficile")** (1.2.0, ADR 0013) | Nel browser (interpretato) 12 mondi stanno sotto ~0,4 s per mossa su desktop; oltre 16 mondi non si guadagna (16 contro 8: 52,2%) |
| Silenzi in L2 (`InferSilence`) | attivo | Chi gioca la prima carta della distribuzione senza accusare non aveva accusi: 51,5% contro senza (1000 partite, non significativo), costo quasi nullo |
| `PriorWeight` L2 | **1,0** | Senza prior, con 8 mondi L2 perde contro L1 (44,6%). Screening: 0 → 44%, 0,2 → 59%, 1 → 65%, 2 → 66%, 4 → 61% |
| Pausa CPU | 450 ms + animazione (0,4–1,3 s) | Leggibilità: l'animazione stessa scandisce le mosse. Mossa CPU nel browser: mediana ~0,9 s |
| Timeout di turno / grazia riconnessione | 30 s / 30 s | ADR 0004 |

## 5. Lezioni apprese

| Problema | Causa | Soluzione / regola |
|---|---|---|
| Migliorare L1 non rende i bot più forti | Con 3 carte in mano e prese obbligatorie le scelte vere sono poche; il risultato tra due L1 è dominato dalla fortuna. Misurato (1.2.0): rischio di presa dell'avversario con la regola dell'asso, maggioranze già decise e valore dinamico dello spincio cambiano il 9,5% delle scelte ma vincono il 50–51% contro la L1 precedente, anche dopo una taratura dei pesi; usati da L2 come politica: 49,5% | Non toccare L1 senza un torneo da migliaia di partite; la forza viene dalla ricerca (L2). Il codice provato è stato tolto: rendeva le simulazioni di L2 due volte più lente |
| Deploy della 1.2.1 fermo: test rosso solo nel job di Pages (la CI della PR era verde) | Il test sulle colonne della tavola usava una partita casuale e dava per scontato che la tavola non fosse mai vuota; con alcune smazzate le CPU la svuotano prima del mio turno | Nei test bUnit che dipendono dalla smazzata fissare il seed (`NavigationManager.NavigateTo("/?seed=N")`) e verificare la precondizione; coprire anche il caso limite (seed 3: tavola vuota) |
| Test online end-to-end rosso solo a volte (`Room.Commitment` nullo appena iniziata la partita) | All'avvio il server mandava prima le carte e poi la stanza con l'impegno: per un attimo il client vedeva la partita senza impegno. Difetto vero, da M7 | 1.3.0: l'impegno parte prima di qualunque carta (`StartCoreAsync`); test `The_commitment_reaches_every_player_before_the_first_cards` sull'ordine dei messaggi |
| `pkill -f` ha chiuso di nuovo la shell (1.2.0) | Il pattern era nella riga di comando della shell stessa | Fermare i processi per PID: `ps -eo pid,args \| grep "[T]uner" \| awk '{print $1}' \| xargs -r kill` |
| Property test lenti (31 s) | `ShouldAllBe`/`ShouldContain(predicato)` di Shouldly compilano un expression tree a ogni chiamata | Nei cicli per-mossa usare LINQ + `ShouldBeEmpty()` |
| Test di architettura rosso su `System.Environment` | Il compilatore genera codice per gli iteratori `yield` che usa `Environment.CurrentManagedThreadId` | Nel motore niente `yield`: restituire array |
| CA1716 su `Declare`, `Me`, `Error` | Sono parole chiave VB | Disattivato per tutta la solution (solo C#) in `Directory.Build.props` |
| Deserializzazione di `Seat` sbagliata | Struct con proprietà solo-get: System.Text.Json usa il costruttore vuoto | Converter dedicato in `SpincioJson` (Seat ↔ numero) |
| Eventi polimorfici su SignalR | `GameEvent` è astratto | Polimorfismo configurato da `SpincioJson` con un modifier; il motore resta senza attributi JSON. Un test controlla che tutti gli eventi siano registrati |
| Rischio di rottura nel publish WASM | `IsTrimmable` su librerie serializzate via reflection | Tolto `IsTrimmable` da Engine, Bots, Contracts |
| PWA offline rotta su GitHub Pages | Riscrivere `index.html` dopo il publish invalida l'hash controllato dal service worker | `<base href>` calcolato a runtime in `index.html`; il service worker usa il proprio `scope` |
| Pulsante "Entra" disabilitato mentre si scrive | `@bind` aggiorna al `change` (perdita del focus) | `@bind:event="oninput"` quando il valore abilita un pulsante |
| "Difficile" più debole di "Normale" | PIMC con pochi mondi è dominato dal rumore | Prior L1 (`PriorWeight`), tarato e confermato su un seed diverso da quello di taratura |
| Test PIMC che crashava | Scenario incoerente con l'ordine di turno (un posto aveva già giocato, i successivi no) | Negli scenari rispettare il giro antiorario e i conteggi delle mani |
| CPU che "resuscita" una partita abbandonata | Il loop delle CPU riprendeva dopo il `delay` con lo stato vecchio | Contatore di generazione; dopo ogni attesa si rivaluta lo stato corrente |
| `pkill -f pattern` ha chiuso la shell stessa | Il pattern compariva anche nella riga di comando della shell | Fermare i processi per ID del task o per porta |
| Playwright: selettore ambiguo | `getByText` trova sia il paragrafo sia il pulsante | Usare `getByRole('button', { name })` |
| Deduzione sbagliata nella spec | "Mai due carte uguali in tavola dopo la prima giocata" è falso (tavola iniziale con coppia + calata) | Corretta in SPEC v1.2; è una proprietà FsCheck |
| Download da Wikimedia rifiutato (HTTP 429) | Wikimedia limita le richieste dall'IP condiviso dell'ambiente cloud | Non aggirare il blocco: il proprietario carica il file nel repo (`assets-source/`) dal browser |
| Nessuna libreria immagini in Python (`pip install pillow` fallisce) | L'ambiente non raggiunge PyPI per Pillow | Ritagli e WebP con il canvas di Chromium via Playwright (`tools/card-slicer/slice.mjs`) |
| Ritagli storti dalla scansione | Carte leggermente ruotate o spostate; la cornice stampata non sempre si rileva | Cornice di misura fissa (mediana): asse X dal centro del disegno, asse Y dalla mediana della riga se la cornice manca |
| Carte che sembravano "scansionate" (storte, cornice stampata visibile, colori spenti) | Ritaglio grezzo della scansione | `slice.mjs` v2: raddrizza ogni carta (angolo che rende più nette le linee della cornice, con penalità sull'inclinazione), taglia dentro la cornice, ridisegna su bianco con margine uniforme, saturazione ×1,25, contrasto ×1,08, maschera di nitidezza. Tra le linee candidate sceglie quella coerente col centro del disegno (X) e con la riga (Y): così un bordo fisico della carta nella scansione non vince |
| Carte "tagliate" dopo la pulizia (punte di spade, corone, piume) | Nelle piacentine il disegno arriva fino alla cornice: tagliare il 3% dentro la cornice lo rosicchiava | Taglio minimo (≈1,3%) e cornice nera ridisegnata sul bordo del disegno: struttura della carta originale, nessun filo di scansione. Attenzione: nella scansione anche il bordo fisico della carta (ombra grigia) è una riga scura, ma la cornice stampata è più nera |
| "La regola nuova non funziona" sul telefono | La PWA tiene in cache la versione precedente finché non si riapre (il service worker aggiorna in background) | Versione delle regole nel piè di pagina ("regole v1.4"), così il proprietario vede quale versione sta giocando; chiudere e riaprire l'app due volte |
| "Non vedo l'applicazione aggiornata" (1.2.0, seconda volta) | Il deploy era riuscito. Su iPhone l'app sulla Home viene ripresa dal background senza ricaricarsi: il browser non ricontrolla il service worker, e una versione già scaricata resta in attesa finché l'app non viene chiusa del tutto | 1.2.1: `reg.update()` a ogni ritorno in primo piano (`visibilitychange`) e barra "Aggiorna" quando la nuova versione è pronta (messaggio `skipWaiting` al service worker, ricarica su `controllerchange`). Verifica: `update.mjs`. Chi ha una versione precedente alla 1.2.1 deve passare a mano una volta sola (chiudere l'app dal multitasking e riaprirla) |
| "Cliccando la x torno alla pagina iniziale" | Nel popup non c'era nessuna ✕: l'unica era quella della barra d'errore di Blazor, il cui "Ricarica" riporta all'inizio. Non riprodotto in Chromium | La barra d'errore ora mostra la prima riga dell'errore (script in `index.html`): con uno screenshot dal telefono si diagnostica. I popup hanno una ✕ vera che chiude e fa continuare |
| L'animazione dei punti non partiva (nessun errore) | `InvokeVoidAsync(id, params object[] args)`: un array di oggetti passato da solo diventa l'array dei parametri, quindi JS riceveva un argomento per ogni elemento | Passare l'array come `object` (un solo argomento); un test bUnit controlla `Arguments.Count == 1` |
| La tavola "cresceva" o si spostava durante la partita | Non per le carte in tavola: cambiavano le misure di ciò che le sta intorno (mano vuota durante il volo, pulsante Accusa, "· mazziere" che andava a capo, dichiarazioni) | Misure fisse per tutto ciò che sta intorno alla tavola; `layout.mjs` lo controlla a ogni mossa |
| Controllo nel browser che segnalava carte "rimaste in volo" | Lo script misurava durante l'animazione della mia carta, quando il resto della mano sembrava ancora giocabile | Mano disattivata durante il volo (anche un difetto per l'utente); i controlli aspettano una carta `playable` visibile e chiudono il riepilogo "Continua" |

## 6. Strategia di test

| Livello | Dove | Cosa garantisce |
|---|---|---|
| Accettazione (AT-xx) | `Engine.Tests/Acceptance` | Le regole del proprietario (contratto) |
| Property-based (FsCheck) | `Engine.Tests/PropertyTests`, `HypotheticalTests` | 40 carte conservate, determinismo, vista = stato per le mosse legali, stati ipotetici fedeli |
| Architettura | `Architecture.Tests` | Dipendenze e confini di informazione (nessuno vede ciò che non deve) |
| Bot | `Bots.Tests` | Comportamenti chiave + tornei (L1 ≥ 80% su L0) |
| Client | `Client.Tests` | Sessione offline, salvataggi, bUnit smoke |
| Server | `Server.Tests` | Stanza con tempo finto, contratti JSON, end-to-end con il vero client SignalR |
| Mutation testing manuale | a ogni milestone | Rompere una regola deve far fallire almeno un test |
| Browser reale | Playwright (vedi RUNBOOK) | Build pubblicata, niente errori in console, tempi CPU |

## 7. Ambiente di sviluppo (cloud)
- Il .NET 10 SDK si installa con il setup script dell'ambiente (vedi `RUNBOOK.md`); `global.json` fissa 10.0.x.
- Host di rete necessari: `dot.net`, `builds.dotnet.microsoft.com`, `api.nuget.org`.
- Chromium e Playwright sono preinstallati (`/opt/pw-browsers`, modulo globale in `/opt/node22/lib/node_modules`).
