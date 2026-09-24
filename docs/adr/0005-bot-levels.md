# ADR 0005 — Livelli dei bot

- **Stato:** Accettato (Fase 3, 24/09/2026)

## Contesto
L'MVP è 2v2 con tre CPU (un compagno e due avversari). Il bot non deve barare: vede solo ciò che vedrebbe un umano.

## Decisione
Interfaccia `IBot.Choose(PlayerView, BotMemory, Pcg32)`: input = vista pubblica + memoria degli eventi pubblici.

| Livello | Descrizione | Quando |
|---|---|---|
| L0 Random | Mossa legale casuale | Test e fuzzing |
| L1 Greedy | Punta a spazzino, denari, settebello, rebello, 7 e carte; penalizza chi lascia in tavola una somma ≤ 10; cooperativo col compagno; dichiara sempre | **MVP** |
| L2 PIMC | ~20 mondi coerenti con memoria e accusi, rollout L1, budget ~300 ms su WASM | Milestone successiva |
| L3 ISMCTS | — | Backlog |

Validazione con `Spincio.Simulator`: L1 batte L0 in ≥ 80% delle partite; L2 batte L1 in ≥ 55%.

## Alternative scartate
| Opzione | Motivo |
|---|---|
| Avversari LLM | Latenza, costo, niente offline |
| Reinforcement learning | Costo di training sproporzionato; PIMC è già risultato il migliore nei progetti open source analizzati |

## Conseguenze
- (+) Bot onesti per costruzione (non hanno accesso a `MatchState`).
- (−) PIMC su WASM single-thread va limitato con budget di tempo e numero di mondi adattivo.
