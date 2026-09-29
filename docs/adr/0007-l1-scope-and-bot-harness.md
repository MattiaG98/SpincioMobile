# ADR 0007 — Perimetro del bot L1 e harness dei bot

- **Stato:** Accettato (M2, 25/09/2026; confermato dal proprietario il 26/09/2026)
- **Integra:** ADR 0005

## Contesto
L'ADR 0005 descrive L1 come "cooperativo col compagno". Implementando M2 è emerso che una cooperazione vera (lasciare prese al compagno, leggere i suoi accusi) richiede di ragionare su più giocate, cioè il lavoro di L2 (PIMC).

## Decisione
1. **Harness nel simulatore, non in `Spincio.Bots`.** Il ciclo "vista → bot → `Apply` → eventi filtrati" ha bisogno di `MatchState`, che i bot non devono mai vedere (regola 5 di CLAUDE.md, verificata da NetArchTest). Il runner vive in `tools/Spincio.Simulator` (`MatchRunner`); in M3 il client avrà il suo equivalente in `LocalGameSession`.
2. **L1 coopera solo in modo implicito:** valuta le prese per la squadra e calcola il rischio solo sull'avversario che gioca subito dopo (il posto successivo è sempre avversario). Nessun segnale al compagno. La cooperazione esplicita passa a L2.
3. **Legalità verificata dal motore.** I bot calcolano le mosse con `SpincioEngine.LegalCommands(PlayerView)`, equivalente per costruzione (e per property test) alla versione sullo stato.
4. **`BotMemory` rifiuta gli eventi non destinati al proprio posto** (lancia eccezione): un bug di filtraggio diventa un errore, non un bot che bara in silenzio.
5. Ogni posto ha il proprio stream PCG32 derivato dal seed della partita: partite tra bot riproducibili.

## Evidenze (Simulator, 1000 partite, lati alternati)
| Confronto | Risultato |
|---|---|
| L1 vs L0 (seed 1 / seed 5000) | 99,3% / 99,1% (criterio: ≥ 80%) |
| L0 vs L0 | 47,7% (nessun vantaggio di lato) |
| L1 vs L1 | 52,0% |
| L1 vs L1 senza rischio spazzino / senza costo calata / valori piatti | 56,5% / 60,4% / 64,1% |

## Conseguenze
- (+) Bot onesti per costruzione e verificati dai test di architettura.
- (−) L1 non "gioca per il compagno": accettabile per l'MVP, da coprire in L2.
