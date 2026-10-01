# ADR 0009 — Grafica delle carte originale in SVG

- **Stato:** Superato da ADR 0011 (01/10/2026)
- **Sostituisce in parte:** la decisione aperta n. 4 del documento di passaggio ("SVG piacentini di Wikimedia Commons")

## Contesto
`CLAUDE.md` ammetteva solo SVG piacentini **di pubblico dominio** da Wikimedia Commons, con verifica file per file. Molte carte italiane su Commons sono però in CC BY-SA (attribuzione e condivisione allo stesso modo), non in pubblico dominio. Inoltre in sviluppo il dominio non era raggiungibile per la verifica.

## Decisione
Carte **disegnate da zero** per Spincio, in SVG generato da componenti Razor (`CardFace`, `SuitGlyph`):
- simboli dei semi originali (denari, coppe, spade, bastoni) con forme distinte, leggibili anche senza colori;
- 1–7 con semi disposti a pip; fante, cavallo e re con una figura stilizzata;
- indici agli angoli (A, 2–7, F, C, R).
Nessun asset esterno: niente download, niente licenze da tracciare.

## Alternative scartate
| Opzione | Motivo |
|---|---|
| Wikimedia Commons | Licenze eterogenee (spesso CC BY-SA), verifica manuale per ogni file, dominio non raggiungibile in sviluppo |
| Edizioni commerciali (Dal Negro, Modiano) | Vietate (diritti e marchi) |

## Conseguenze
- (+) Zero rischio legale, peso minimo, nitidezza a qualunque risoluzione.
- (−) Aspetto meno "tradizionale" delle piacentine storiche: si può migliorare il disegno in seguito senza toccare la logica.
