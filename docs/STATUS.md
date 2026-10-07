# Stato del progetto

> Da aggiornare **a fine di ogni milestone** (regola in `CLAUDE.md`). Ultimo aggiornamento: **07/10/2026**.
> Fonte delle regole: [`SPEC.md`](SPEC.md) · decisioni: [`adr/`](adr/) · conoscenza tecnica: [`KNOWLEDGE.md`](KNOWLEDGE.md) · procedure: [`RUNBOOK.md`](RUNBOOK.md)

## Milestone

| # | Milestone | Stato | Verifica |
|---|---|---|---|
| M0 | Solution, CI, test di architettura | ✅ Fatto | CI verde; test di architettura provato con violazioni intenzionali |
| M1 | Motore delle regole (AT-01…AT-32) | ✅ Fatto | Tutti gli AT verdi ([`AT-MAP.md`](AT-MAP.md)); 11 regole rotte di proposito, tutte intercettate |
| M2 | Bot L0/L1 + simulatore | ✅ Fatto | L1 batte L0 nel **99,3%** (criterio ≥ 80%) |
| M3 | UI PWA offline | ✅ Fatto | Partita completa in Chromium headless; salvataggio e ripresa |
| M4 | Rilascio: GitHub Pages, crediti, correzioni | ✅ Pronto | Workflow `pages.yml` pronto; **manca l'attivazione di Pages** (vedi Decisioni aperte) |
| M5 | Bot L2 (PIMC) + livello di difficoltà | ✅ Fatto | L2 batte L1 nel **64,4%** (16 mondi) / **62,0%** (8 mondi, browser); criterio ≥ 55% |
| M6 | Grafica delle carte + animazioni | ✅ Fatto | Carte SVG originali (ADR 0009); animazioni con `prefers-reduced-motion` |
| M7 | Online (SignalR, server autoritativo) | ✅ Fatto in locale | 2 giocatori reali + 2 CPU nel browser; test end-to-end; **non pubblicato** (hosting a pagamento escluso, ADR 0010) |
| M8 | App nativa (store) | ⏭️ Saltato | Richiede account sviluppatore a pagamento: opzioni in [`NATIVE.md`](NATIVE.md) |
| M9 | Rifinitura dopo il playtest del proprietario | ✅ Fatto | **A** ✅ storico mosse a scomparsa, nomi CPU (Titti compagno, Tito e Vava avversari), frasi in seconda persona per il giocatore · **B** ✅ carte dalla scansione piacentina di Wikimedia Commons, senza indici (ADR 0011, decisione del proprietario) |
| M10 | Animazioni delle mosse (prima tranche del refactor UI) | ✅ Fatto (PR #4) | Ogni carta giocata vola dal giocatore al tavolo; nelle prese si posa sulle carte prese, le evidenzia e le porta al giocatore. Offline e online; rispetta `prefers-reduced-motion` |
| M11 | Carte "da app" (seconda tranche) | ✅ Fatto | PR #5–#7: raddrizzate, fondo bianco, nessun taglio del disegno, cornice ridisegnata; dalla scansione originale 3507×2417 caricata dal proprietario (cornice 273 px → nessun ingrandimento) |
| M12 | Interfaccia "da gioco mobile" (terza tranche) | ✅ Fatto (PR #9, #10) | Menu iniziale, Opzioni, menu di pausa ☰, popup con ✕, dettaglio errori; pagina **Regole** (dal menu e dalla pausa) riassunta da SPEC v1.4 |
| — | Regola P8 "asso pigliatutto" (SPEC v1.3 → v1.4, dettata dal proprietario) | ✅ v1.3 (PR #6); v1.4 in revisione: con un asso in tavola si prende solo quello | AT-33…AT-39; versione delle regole nel piè di pagina | AT-33…AT-38; salvataggi v1.2 non ripresi |

## Metriche

| Voce | Valore | Come si rimisura |
|---|---|---|
| Test automatici | **197** (Engine 85, Bots 31, Client 37, Architecture 23, Server 21) | `dotnet test Spincio.slnx` |
| L1 vs L0 | 92,9% (seed 1, regole v1.3 con asso pigliatutto); era 99,3% con la v1.2 | `--x Greedy --y Random` |
| L2 vs L1 | 58,0% (8 mondi, 300 partite, seed 5000, regole v1.3); con la v1.2: 64,4% (16 mondi) / 62,0% (8 mondi) | `--x Pimc --y Greedy --worlds N` |
| L2 senza prior L1 | 56,0% (16 mondi) / **44,6%** (8 mondi): peggio di L1 | `PimcOptions.PriorWeight = 0` |
| Tempo decisione L2 (.NET) | media 3 ms, max 65 ms (8 mondi) | `--timing Pimc --worlds 8` |
| Mossa CPU "Difficile" nel browser | mediana 944 ms, max 2172 ms (02/10): 450 ms di pausa voluta + animazione della carta | Playwright, vedi RUNBOOK |
| Download dell'app (Brotli) | ~3,0 MB | `dotnet publish`, somma dei `.br` in `_framework` |

## Decisioni aperte

| # | Decisione | Proposta | Chi |
|---|---|---|---|
| 1 | Attivare GitHub Pages | Settings → Pages → Source: **GitHub Actions**; poi merge su `main` | Proprietario |
| 2 | Hosting del server online | Rimandato (ADR 0010). Opzioni gratuite o quasi in `RUNBOOK.md` | Proprietario |
| 3 | ~~Carte~~ | ✅ 01/10: scansione piacentina (ADR 0011). Rischio diritti accettato dal proprietario; se arriva una segnalazione si torna al ridisegno originale | — |
| 4 | ~~Icone dell'app~~ | ✅ Fatto il 28/09: icona originale (`Assets/icon.svg`) | — |
| 5 | Verifica del marchio "Spincio" (EUIPO/UIBM) | Prima di qualsiasi pubblicazione sugli store | Proprietario |

## Prossimi passi consigliati
1. Playtest del proprietario su telefono (offline, entrambi i livelli), segnalando il numero di partita in caso di dubbi.
2. Attivare Pages e fare il merge della PR su `main`.
3. Se si vuole l'online pubblico: scegliere l'hosting (ADR 0010) e impostare `ServerUrl` in `wwwroot/appsettings.json`.
4. **Tutorial visivo** (richiesto dal proprietario): da fare **quando la grafica sarà completata**.
5. Backlog regole: Spincione (due mazzi, bàgher), tutti contro tutti a 3, altri mazzi regionali — **da raccogliere dal proprietario**, niente regole inventate.

## Registro

| Data | Cosa |
|---|---|
| 24/09/2026 | Fasi 0–3 (regolamento, spec, mercato, architettura); setup del repo |
| 25/09/2026 | M0, M1, M2 |
| 26/09/2026 | M3 |
| 28/09/2026 | M4, M5, M6, M7; M8 documentato e saltato; file di tracciamento (questo, KNOWLEDGE, RUNBOOK, NATIVE); icone originali |
| 29/09–01/10/2026 | Merge PR #1, GitHub Pages attivo (https://mattiag98.github.io/SpincioMobile/), app installata su iPhone dal proprietario; M9-A (PR #2); M9-B carte piacentine |
