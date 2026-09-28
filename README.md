# Spincio

Gioco di carte **Spincio** (variante di casa dello Spazzino) in 2v2, con mazzo piacentino da 40 carte.

- **Offline (PWA installabile):** tu + compagno CPU contro 2 CPU, livelli *Normale* (L1) e *Difficile* (L2, Monte Carlo).
- **Online:** stanze con codice per giocare con gli amici; server autoritativo, CPU al posto dei disconnessi, seed verificabile a fine partita. Pronto in locale; per giocare in rete serve un hosting (vedi `docs/RUNBOOK.md`).
- **Carte:** disegni SVG originali ispirati alle piacentine.

## Documentazione
- [Regolamento e test di accettazione](docs/SPEC.md) — fonte di verità delle regole · [mappa AT → test](docs/AT-MAP.md)
- [Stato del progetto](docs/STATUS.md) — milestone, metriche, decisioni aperte
- [Base di conoscenza](docs/KNOWLEDGE.md) — architettura, numeri tarati, lezioni apprese
- [Runbook](docs/RUNBOOK.md) — avvio, test, deploy, verifica delle partite online
- [Decisioni architetturali](docs/adr/) · [App nativa: opzioni](docs/NATIVE.md) · [Crediti](CREDITS.md)
- [Regole per lo sviluppo](CLAUDE.md)

## Avvio in locale
```
dotnet run --project src/Spincio.Client          # gioco offline: http://localhost:5058
dotnet run --project src/Spincio.Server          # server online (opzionale): http://localhost:5080
```
Aggiungendo `?seed=N` all'URL si rigioca la stessa distribuzione (il numero è mostrato in fondo alla pagina come "Partita n.").

## Stack
Blazor WebAssembly PWA (.NET 10) · motore C# puro condiviso · ASP.NET Core + SignalR (online) · xUnit + Shouldly + FsCheck.
