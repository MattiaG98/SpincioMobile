# ADR 0003 — Persistenza come seed + log dei comandi

- **Stato:** Accettato (Fase 3, 24/09/2026)

## Contesto
Serve salvare/riprendere partite offline, fare debug di bug di regole e ricostruire lo stato per la riconnessione online.

## Decisione
Una partita è persistita come **seed + lista ordinata dei comandi**. Lo stato si ricostruisce con `fold(Apply)` a partire da `NewMatch(seed)`.

## Alternative scartate
| Opzione | Motivo |
|---|---|
| Snapshot dello stato | Più fragile ai cambi di modello, niente replay |
| Event sourcing completo | Eccessivo: gli eventi sono già derivabili dai comandi |

## Conseguenze
- (+) Replay e riproduzione esatta dei bug (basta allegare seed + log).
- (+) Log molto piccolo (≤ ~40 comandi per smazzata).
- (−) Una modifica alle regole invalida i log salvati: il log porta la versione del regolamento; versioni diverse non si ricaricano.
