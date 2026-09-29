# ADR 0004 — Online con server autoritativo su SignalR

- **Stato:** Accettato (Fase 3, 24/09/2026) — implementazione nella milestone online, non nell'MVP

## Contesto
Le carte prese e le mani altrui sono informazioni nascoste (E3, A4, AT-31). Il market research mostra lamentele su "carte truccate" e abbandoni.

## Decisione
- ASP.NET Core + **SignalR**, **server autoritativo**.
- Una `GameRoom` per partita con coda seriale (`Channel<T>`).
- Il client invia solo intenzioni con `expectedSeq`; il server valida con il motore e invia gli eventi filtrati per posto tramite `Audience`.
- Riconnessione: snapshot `ViewFor(seat)`.
- Disconnessione: bot L1 dopo 30 s di grazia; timer di turno 30 s.
- Astrazione `IGameSession` con `LocalGameSession` (MVP) e `RemoteGameSession` (SignalR): UI identica.

## Alternative scartate
| Opzione | Motivo |
|---|---|
| P2P | Nessuna autorità: cheating banale |
| Orleans / RabbitMQ | Complessità non giustificata per il carico previsto |

## Conseguenze
- (+) Anti-cheat nel motore (filtro per `Audience`), non nella UI.
- (−) Serve hosting e DB (PostgreSQL; SQLite in dev).
