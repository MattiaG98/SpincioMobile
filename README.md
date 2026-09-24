# Spincio

Gioco di carte **Spincio** (variante di casa dello Spazzino) in 2v2, con mazzo piacentino da 40 carte.

- **MVP:** web/PWA offline — umano + compagno CPU contro 2 CPU.
- **Poi:** AI più forte, carte piacentine e animazioni, multiplayer online, wrapper nativo Android/iOS.

## Documentazione
- [Regolamento e test di accettazione](docs/SPEC.md) — fonte di verità delle regole.
- [Decisioni architetturali](docs/adr/)
- [Regole per lo sviluppo](CLAUDE.md)

## Stack
Blazor WebAssembly PWA (.NET 10) · motore C# puro condiviso · ASP.NET Core + SignalR (online) · xUnit + Shouldly + FsCheck.
