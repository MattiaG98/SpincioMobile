# Stato del progetto

> Da aggiornare **a fine di ogni milestone** (regola in `CLAUDE.md`). Ultimo aggiornamento: **13/10/2026**.
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
| M13 | Versione 1.0.0 e schermo di gioco fisso | ✅ Fatto | Versione nel menu (ADR 0012, `CHANGELOG.md`); niente scorrimento; tavola di misura fissa con carte che si rimpiccioliscono (fino a 16, `layout.mjs` su 3 schermi); carta giocata che si rimpicciolisce verso la tavola |
| M14 | Animazione dei punti (1.1.0) | ✅ Fatto | "+N" vola sul punteggio della squadra per spazzini, accusi e fine smazzata (alla chiusura del riepilogo); `points.mjs` nel browser |
| M15 | Statistiche e CPU più forti (1.2.0) | ✅ Fatto | Pannello Statistiche (menu e pausa); "Normale" = L2 4 mondi (59,7% contro il vecchio), "Difficile" = L2 12 mondi + silenzi (53,1%); accusi delle CPU verificati su partite intere (ADR 0013) |
| — | Scritta MARIANA (1.3.0, dettata dal proprietario) | ✅ Fatto | Fante che prende 5 e 3: scritta MARIANA, nessun punto; al posto di "Spazzino!" se svuota la tavola (lo spazzino vale). AT-40, pagina Regole |
| M16 | Online con server temporaneo (1.6.0) | ✅ Fatto (da provare in un codespace vero) | Indirizzo del server nel link di invito, "Invita gli amici", codespace che avvia il server da solo, battito del server mentre si gioca (ADR 0014); punti più piccoli e rapidi |
| M17 | Giocatori online e inviti (1.7.0) | ✅ Fatto (da provare in un codespace vero) | Lista "Giocatori online" con "Invita"; notifica in alto in ogni schermata con Accetta/Rifiuta e scelta "Con"/"Contro" (ADR 0015); test end-to-end con due client e `lobby.mjs` con tre giocatori nel browser |
| M18 | Server fisso su Render (1.7.1 → 1.8.1) | ✅ Fatto | Immagine Docker, `render.yaml` e workflow "Server image" (ADR 0016). Servizio creato dal proprietario il 15/10 (`/health` ok), indirizzo `https://spincio-server.onrender.com` in `ServerUrl` dalla 1.8.1 |
| M19 | Partite che sopravvivono a un riavvio del server (1.8.0) | ✅ Fatto | Punto di ripristino sigillato (AES-GCM) nei client, `Resume` sull'hub, ricostruzione con il motore e verifica finale che regge (ADR 0017); test end-to-end con riavvio vero del server |
| M20 | Modalità Spincione (1.9.0, regole v1.5) | ✅ Fatto | Regole dettate dal proprietario (SPEC §1.7, AT-41…AT-50, ADR 0018); motore con due mazzi e Spincio identico al bit (AT-50); CPU e simulatore; menu, pagina Online, inviti, Regole; online con punto di ripristino; `layout.mjs … spincione` su 3 schermi |
| M21 | Gemella nello Spincione (1.10.0, regole v1.6) | ✅ Fatto | Dettata dal proprietario (SP10, AT-51): la carta che prende la gemella vale 2, con lo spazzino 3 ("Gemella con cappella"); riepilogo, pagina Regole, CPU (peso 20); salvataggi di Spincione 1.9.0 non ripresi |
| — | Regola P8 "asso pigliatutto" (SPEC v1.3 → v1.4, dettata dal proprietario) | ✅ v1.3 (PR #6); v1.4 in revisione: con un asso in tavola si prende solo quello | AT-33…AT-39; versione delle regole nel piè di pagina | AT-33…AT-38; salvataggi v1.2 non ripresi |

## Metriche

| Voce | Valore | Come si rimisura |
|---|---|---|
| Test automatici | **352** (Engine 137, Bots 39, Client 113, Architecture 23, Server 40) | `dotnet test Spincio.slnx` |
| L1 vs L0 | 92,9% (seed 1, regole v1.3 con asso pigliatutto); era 99,3% con la v1.2. Spincione: 96,0% (regole v1.6) | `--x Greedy --y Random [--variant Spincione]` |
| L2 vs L1, Spincione | 53,2% (4 mondi, regole v1.6); con la v1.5 51,2% (4 mondi) / 52,7% (12 mondi), sempre su 400 partite: le partite durano ~2 smazzate e la fortuna pesa di più | `--x Pimc --y Greedy --variant Spincione` |
| L2 vs L1 | 57,8% (4 mondi, 800 partite, seed 9000); 55,5% (8) / 60,3% (16) / 60,3% (32) su 400 partite, seed 5000 | `--x Pimc --y Greedy --worlds N` |
| Livelli 1.2.0 contro 1.1.0 | "Normale" 59,7% · "Difficile" 53,1% (1000 partite ciascuno) | progetto di confronto, vedi ADR 0013 |
| L2 16 mondi vs 8 | 52,2% (600 partite): più mondi aiutano poco | `--x Pimc --y Pimc --worlds 16 --yworlds 8` |
| L2 senza prior L1 | 56,0% (16 mondi) / **44,6%** (8 mondi): peggio di L1 | `PimcOptions.PriorWeight = 0` |
| Tempo decisione L2 (.NET) | media 2,8 ms (4 mondi), 4,6 ms (16 mondi); max 74 / 104 ms | `--timing Pimc --worlds N` |
| Decisione CPU nel browser | "Normale" (4 mondi) mediana 29 ms, max 0,27 s; "Difficile" (12 mondi) mediana 33 ms, p90 150 ms, max 0,39 s — Chromium desktop | `cpu-time.mjs` |
| Mossa CPU "Difficile" nel browser | mediana 944 ms, max 2172 ms (02/10): 450 ms di pausa voluta + animazione della carta | Playwright, vedi RUNBOOK |
| Traffico del server per partita | ~270 KB per persona collegata (1,1 MB per una partita in 4); metà sono i punti di ripristino (max 1,4 KB l'uno). Lista online a riposo: ~11 KB/ora per app aperta | misura del 15/10 su 15 partite complete (messaggi SignalR JSON, senza compressione) |
| Capacità del piano gratuito Render | 5 GB di traffico al mese ≈ 17.000 "partite-persona": ~4.300 partite in 4, ~8.700 in 2 + 2 CPU. Ore: 750 al mese, bastano per un server sempre acceso. Contemporanee: ~15–20 partite con CPU (0,1 CPU), di più senza CPU | `docs/KNOWLEDGE.md`, "Server fisso su Render" |
| Download dell'app (Brotli) | ~3,0 MB | `dotnet publish`, somma dei `.br` in `_framework` |

## Decisioni aperte

| # | Decisione | Proposta | Chi |
|---|---|---|---|
| 1 | Attivare GitHub Pages | Settings → Pages → Source: **GitHub Actions**; poi merge su `main` | Proprietario |
| 2 | ~~Hosting del server online~~ | ✅ 15/10: server fisso su Render, piano gratuito (ADR 0016), `https://spincio-server.onrender.com`. Da controllare una volta: il Blueprint su Render deve seguire il ramo `main`. Il codespace (ADR 0014) resta come riserva | Proprietario |
| 7 | ~~Partite che sopravvivono a un riavvio del server~~ | ✅ 15/10: fatto nella 1.8.0 (ADR 0017) | — |
| 6 | Notifiche push per gli inviti | Oggi l'invito arriva solo con l'app aperta (ADR 0015). Le push richiedono app sulla Home, permesso e un servizio di invio: da decidere se servono | Proprietario |
| 3 | ~~Carte~~ | ✅ 01/10: scansione piacentina (ADR 0011). Rischio diritti accettato dal proprietario; se arriva una segnalazione si torna al ridisegno originale | — |
| 4 | ~~Icone dell'app~~ | ✅ Fatto il 28/09: icona originale (`Assets/icon.svg`) | — |
| 5 | Verifica del marchio "Spincio" (EUIPO/UIBM) | Prima di qualsiasi pubblicazione sugli store | Proprietario |

## Prossimi passi consigliati
1. Playtest del proprietario su telefono (offline, entrambi i livelli), segnalando il numero di partita in caso di dubbi.
2. Attivare Pages e fare il merge della PR su `main`.
3. Se si vuole l'online pubblico: scegliere l'hosting (ADR 0010) e impostare `ServerUrl` in `wwwroot/appsettings.json`.
4. **Tutorial visivo** (richiesto dal proprietario): da fare **quando la grafica sarà completata**.
5. Backlog regole: tutti contro tutti a 3, altri mazzi regionali — **da raccogliere dal proprietario**, niente regole inventate. (Spincione fatto nella 1.9.0; il "bàgher" non esisteva.)

## Registro

| Data | Cosa |
|---|---|
| 24/09/2026 | Fasi 0–3 (regolamento, spec, mercato, architettura); setup del repo |
| 25/09/2026 | M0, M1, M2 |
| 26/09/2026 | M3 |
| 28/09/2026 | M4, M5, M6, M7; M8 documentato e saltato; file di tracciamento (questo, KNOWLEDGE, RUNBOOK, NATIVE); icone originali |
| 29/09–01/10/2026 | Merge PR #1, GitHub Pages attivo (https://mattiag98.github.io/SpincioMobile/), app installata su iPhone dal proprietario; M9-A (PR #2); M9-B carte piacentine |
| 02/10–07/10/2026 | M10–M12, regola P8 (SPEC v1.4), "napola" → "spincio"; versione 1.0.0 (M13) |
| 08/10/2026 | Animazione dei punti, versione 1.1.0 (M14); statistiche e CPU più forti, versione 1.2.0 (M15) |
| 09/10/2026 | Barra "Aggiorna" e controllo degli aggiornamenti al ritorno in primo piano, versione 1.2.1; scritta MARIANA, versione 1.3.0; pannello Contributi (Megako, Nandone), versione 1.4.0; avatar delle CPU, versione 1.5.0; capelli rossi ricci per tutti, 1.5.1 |
| 11/10/2026 | Avatar tutti uomini con capelli rossi ricci, Tito di colore (scelte del proprietario), versione 1.5.2; accuso: niente etichetta dopo il clic, pulsante compatto, 1.5.3; punti che partono insieme alla mossa, 1.5.4 |
| 13/10/2026 | Online con server temporaneo su Codespaces e link di invito, versione 1.6.0 (M16); giocatori online e inviti con scelta "con/contro", versione 1.7.0 (M17); server fisso su Render preparato, versione 1.7.1 (M18) |
| 13/10–15/10/2026 | CPU che tengono l'asso a tavola vuota (1.7.2); partite che sopravvivono a un riavvio del server e aggiornamento di sicurezza SignalR (1.8.0, M19); server Render attivo e nell'app (1.8.1) |
| 17/10/2026 | Modalità Spincione, regole v1.5 (M20, versione 1.9.0); gemella, regole v1.6 (M21, versione 1.10.0) |
