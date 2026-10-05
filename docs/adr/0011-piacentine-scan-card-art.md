# ADR 0011 — Carte dalla scansione piacentina di Wikimedia Commons

- **Stato:** Accettato (M9, 01/10/2026) — decisione del proprietario
- **Supera:** ADR 0009 (carte SVG originali)

## Contesto
Dopo il playtest il proprietario vuole le carte piacentine "vere", non un disegno ispirato. Su Wikimedia Commons il mazzo completo è disponibile come un'unica scansione: `File:Carte_piacentine_al_completo.jpg` (utente Florixc, 07/09/2009, 3507 × 2417 px; in `assets-source/` dal 05/10/2026 a piena risoluzione). La scansione è dichiarata di pubblico dominio da chi l'ha caricata.

Il Tech lead ha fatto presente, prima della decisione:
- la scansione riproduce un mazzo moderno in commercio; chi scansiona non detiene i diritti sul disegno, quindi la dichiarazione di pubblico dominio non tutela davvero;
- il repository e il sito su GitHub Pages sono pubblici: le immagini sono distribuite, non solo usate in privato;
- l'alternativa (ridisegno originale in stile piacentino) era pronta come anteprima.

Il proprietario ha deciso di usare comunque la scansione ("progetto personale").

## Decisione
1. Le 40 carte sono ritagliate dalla scansione con `tools/card-slicer` e salvate come `src/Spincio.Client/wwwroot/cards/<notazione>.webp` (es. `7D.webp`, `KB.webp`).
2. Carte "pulite": nessun indice aggiunto agli angoli, come le piacentine stampate.
3. Il service worker mette in cache anche i `.webp`: le carte funzionano offline.
4. Fonte, autore e licenza dichiarata sono elencati in `CREDITS.md`.
5. Il divieto di `CLAUDE.md` sulle grafiche degli editori resta per tutto il resto; questa ADR è l'unica eccezione.

## Conseguenze
- (+) Aspetto tradizionale, riconoscibile da chi gioca con le piacentine.
- (−) Rischio di diritti di terzi: se arriva una segnalazione (per esempio una richiesta di rimozione a GitHub), si torna al ridisegno originale (commit `678e1fd` sul ramo di lavoro) con una nuova ADR.
- (−) Circa 1–2 MB in più al primo download; nessun effetto sul motore, sui bot o sul server.
- Sullo store (M8) questa scelta andrebbe rivista: lì il rischio non è accettabile.
