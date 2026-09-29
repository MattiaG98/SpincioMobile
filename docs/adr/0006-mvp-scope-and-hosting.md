# ADR 0006 — Perimetro dell'MVP e hosting

- **Stato:** Accettato (Fase 4, 25/09/2026)
- **Integra:** ADR 0001, ADR 0004 (non li sostituisce)

## Contesto
MVP in circa una settimana: motore + bot L1 + UI PWA offline in 2v2. La struttura della solution definita in Fase 3 include progetti che servono solo all'online.

## Decisione
1. `Spincio.Server`, `Spincio.Contracts` e i relativi test **non vengono creati** fino alla milestone online (M7). Le regole di dipendenza restano in `CLAUDE.md` e verranno verificate da NetArchTest quando i progetti esisteranno.
2. I test di architettura vivono in un progetto dedicato `tests/Spincio.Architecture.Tests`.
3. bUnit nell'MVP: solo **smoke test** (M4). Le regole sono coperte dagli AT del motore.
4. Hosting dell'MVP: **GitHub Pages** (sito statico, gratuito, deploy dalla CI).
5. Versioni dei pacchetti gestite centralmente (`Directory.Packages.props`), SDK fissato da `global.json` (10.0.x, `latestFeature`).

## Conseguenze
- (+) Meno progetti da mantenere durante la settimana dell'MVP.
- (+) Deploy senza costi né server.
- (−) GitHub Pages serve l'app sotto un sotto-percorso: in M4 va impostato `<base href>` e il fallback 404 per il routing SPA.
