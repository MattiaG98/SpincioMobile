# ADR 0008 — Bot L2 (PIMC) e stati ipotetici

- **Stato:** Accettato (M5, 28/09/2026)
- **Integra:** ADR 0005, ADR 0007. Precisa la regola 5 di `CLAUDE.md`.

## Contesto
L2 usa Perfect Information Monte Carlo: per ogni decisione immagina alcune distribuzioni possibili delle carte che non vede, gioca ogni mossa candidata in ciascuna e sceglie la migliore in media. Per simulare serve uno stato di partita completo, mentre la regola 5 vieta ai bot di ricevere `MatchState`.

## Decisione
1. **Il bot non riceve mai lo stato reale.** `IBot.Choose` riceve solo `PlayerView`, `BotMemory` e il proprio `Pcg32` (test di architettura `Bots_are_given_only_a_player_view`).
2. **Stati ipotetici solo dal motore e solo a partire dalla vista.** `SpincioEngine.Hypothetical(view, guess, seed)` costruisce uno stato che coincide con tutto ciò che il posto sa (mano, tavola, numero di carte altrui, mazzo, punteggio, accusi) e prende dall'ipotesi solo le parti nascoste. Rifiuta ipotesi incoerenti con la vista o che non contano esattamente le 40 carte.
3. **Solo `PimcBot` tocca `MatchState`** nel progetto Bots (test `Only_the_search_bot_handles_match_states`).
4. **Informazioni usate da L2** (le stesse di un giocatore con memoria perfetta):
   - carte viste nella smazzata (tavola iniziale, mani proprie, giocate e prese);
   - chi ha preso cosa (le prese avvengono sul tavolo; i mazzetti restano nascosti, E3);
   - accusi dichiarati nella distribuzione corrente: i mondi campionati devono essere coerenti con tipo e punti (campionamento per rifiuto, con un numero massimo di tentativi).
   Nessuna inferenza negativa dal mancato accuso: un umano può dimenticarsi di dichiarare.
5. **Valutazione:** differenza di punti a fine smazzata tra la propria squadra e l'altra (compresi spazzini e accusi della simulazione), più un bonus per "tutti i denari" (F7). I rollout usano L1 per tutti e quattro i posti, compagno compreso: la cooperazione col compagno è implicita nella simulazione (chiude il punto 2 dell'ADR 0007).
6. **Budget deterministico** (numero di mondi, non tempo): le partite tra bot restano riproducibili.
7. **Prior L1:** al valore medio dei mondi si somma `PriorWeight × L1.Evaluate` (default 1,0). Con pochi mondi PIMC puro è dominato dal rumore.

## Evidenze (Simulator, 1000 partite, lati alternati)
| Configurazione | L2 vs L1 |
|---|---|
| PIMC puro, 16 mondi / 8 mondi (seed 1) | 56,0% / **44,6%** (peggio di L1) |
| Screening del prior con 8 mondi (500 partite, seed 1): 0 / 0,2 / 0,5 / 1 / 2 / 4 | 44% / 59% / 63% / 65% / 66% / 61% |
| **Scelta: prior 1,0**, conferma su seed nuovo (5000): 16 mondi / 8 mondi | **64,4% / 62,0%** (criterio ≥ 55%) |

Tempi: in .NET una decisione richiede in media 3 ms (max 65 ms con 8 mondi). Nel browser (WebAssembly interpretato) la mossa della CPU "Difficile" arriva in 756 ms di mediana e 938 ms al massimo, compresi i 700 ms di pausa voluta.

## Conseguenze
- (+) Un bot più forte senza barare, con l'onestà garantita da API e test di architettura.
- (−) Il browser usa 8 mondi invece di 16: il livello "Difficile" è un po' meno forte che nel simulatore (62% contro 64%).
