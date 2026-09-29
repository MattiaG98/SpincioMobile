# ADR 0001 — Client Blazor WebAssembly standalone PWA

- **Stato:** Accettato (Fase 3, 24/09/2026)

## Contesto
MVP web entro una settimana, poi Android/iOS. Lo sviluppatore conosce .NET/Blazor. Il motore deve girare sia nel client (offline vs CPU) sia nel server (online autoritativo).

## Decisione
Client **Blazor WebAssembly standalone**, installabile come **PWA**, su **.NET 10 LTS**. Il motore C# (`Spincio.Engine`) è condiviso tra client e server.

## Alternative scartate
| Opzione | Motivo |
|---|---|
| MAUI Blazor Hybrid | Rimandato: eventuale wrapper nativo dopo l'MVP |
| Blazor Server | Niente offline |
| React/TS | Motore da duplicare in due linguaggi |
| Unity / Godot | Curva di apprendimento, peso, overkill per un gioco di carte |

## Conseguenze
- (+) Un solo linguaggio, un solo motore, offline nativo.
- (−) Download WASM pesante: trimming + Brotli, niente AOT nell'MVP.
- (−) Storage PWA su iOS volatile: perdita accettata nell'MVP.
