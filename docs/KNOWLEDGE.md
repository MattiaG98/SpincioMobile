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
| `Spincio.Bots` | L0 casuale, L1 greedy, L2 PIMC | Ricevono solo `PlayerView` + `BotMemory`; solo `PimcBot` tocca stati **ipotetici** (ADR 0008) |
| `Spincio.Contracts` | DTO SignalR, `CommandCodec`, `SpincioJson`, `Commitment` | Condiviso da Client e Server; nessuna dipendenza da Bots/Client/Server |
| `Spincio.Client` | PWA Blazor WebAssembly | La UI vede solo `IGameSession` (vista del giocatore); mai `MatchState` |
| `Spincio.Server` | Stanze online autoritative | Una coda seriale per stanza; timer con `TimeProvider` |
| `tools/Spincio.Simulator` | Tornei tra bot, tempi di decisione | Contiene `MatchRunner` (usa `MatchState`, quindi non sta in Bots) |

Tutte queste regole sono **verificate da test** in `tests/Spincio.Architecture.Tests`.

## 2. Flussi principali

### Una mossa, offline
`Home.razor` → `IGameSession.PlayAsync(cmd)` → `LocalGameSession` → `SpincioEngine.Apply(state, cmd)` → nuovo stato + eventi → eventi filtrati per il giocatore (`EventsFor`) → feed, riepilogo, salvataggio (seed + log comandi) → loop delle CPU con pausa di 700 ms.

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
| Eventi su SignalR | JSON con discriminatore `$kind`; `Seat` come numero | `SpincioJson` |
| Impegno del seed | `SHA-256("{seed}:{salt}")` in esadecimale minuscolo | `Commitment.Of` |

## 4. Numeri tarati (e perché)

| Parametro | Valore | Motivo |
|---|---|---|
| Pesi di L1 (`GreedyWeights`) | carta 1, denaro +1, sette +1,5, settebello +5, rebello +4, spazzino 10, rischio spazzino 8, costo calata 0,3 | Ablazione M2: ogni componente fa vincere; ×2 e ×0,5 sul rischio peggiorano |
| Mondi L2 | 16 nel simulatore/server, **8 nel browser** | Nel browser (interpretato) 8 mondi stanno sotto ~250 ms per mossa |
| `PriorWeight` L2 | **1,0** | Senza prior, con 8 mondi L2 perde contro L1 (44,6%). Screening: 0 → 44%, 0,2 → 59%, 1 → 65%, 2 → 66%, 4 → 61% |
| Pausa CPU | 700 ms | Leggibilità per l'umano |
| Timeout di turno / grazia riconnessione | 30 s / 30 s | ADR 0004 |

## 5. Lezioni apprese

| Problema | Causa | Soluzione / regola |
|---|---|---|
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
