# ADR 0019 — CPU L3: ragiona sulle carte non uscite e cerca fino a fine distribuzione

- **Stato:** Accettato (12/11/2026, versione 1.12.0)
- **Modifica:** ADR 0013 (entrambi i livelli usavano L2). **Estende:** ADR 0008 (stati ipotetici: ora anche L3 li usa).
- **Richiesta del proprietario:** "La IA del compagno deve cambiare, attualmente gioca le carte senza un senso: deve giocare
  le carte che conviene giocare in base a quelle che non sono ancora uscite e potrebbero essere giocate dagli avversari.
  Dobbiamo creare una IA che ragioni in base al gioco ed applichi la strategia migliore per vincere."

## Contesto
Analisi della CPU di prima (L2, PIMC), fatta con il simulatore e con un "giudice" nuovo (`--audit`): il giudice guarda le carte
vere, che le CPU non vedono, per giudicare ogni scelta.

- **La ricerca di L2 contava poco.** L2 simulava ogni mossa fino a fine smazzata con L1 per tutti, in pochi mondi (4 a
  "Normale"). Senza l'aiuto della valutazione a una mossa di L1 perdeva contro L1 stesso (44,6%, ADR 0013). Quell'aiuto
  però è in "unità L1" (uno spazzino = 10) e veniva sommato a una media in punti: valeva dieci volte la ricerca. In pratica
  L2 giocava come L1 più una correzione rumorosa.
- **Gli errori del compagno erano quelli di L1.** L1 stima il rischio di lasciare uno spazzino solo dalla somma della
  tavola e non vede l'asso pigliatutto (P8). L2 a "Normale" calava un 7 il doppio delle volte di L1 (3,6% contro 1,9%
  delle scelte).

## Decisione
Una CPU nuova, **L3** (`LookaheadBot`), per tutte e tre le CPU dell'app (compagno compreso) e per i posti vuoti online.

1. **Carte non uscite.**
   - Per ogni decisione distribuisce a caso le carte che non ha visto in mondi possibili (`WorldSampler`, lo stesso
     campionatore di L2, estratto da `PimcBot`).
   - I mondi sono coerenti con tutto ciò che sa: carte uscite, carte in mano agli altri, accusi fatti e silenzi (chi gioca
     la prima carta della distribuzione senza accusare non aveva niente da accusare).
2. **Ricerca fino a fine distribuzione.**
   - In ogni mondo prova ogni giocata e cerca tutte le risposte fino a fine distribuzione, più la prima giocata della
     distribuzione dopo.
   - Ogni posto gioca al meglio: la nostra squadra massimizza, gli avversari minimizzano (minimax con potature alfa-beta).
   - Sceglie la carta migliore in media sui mondi.
3. **Valore dei punti attesi a fine smazzata** (`RoundOutlook`).
   - Le carte prese valgono per i punti che porteranno, supponendo che ciascuna carta ancora da prendere vada all'una o
     all'altra squadra con la stessa probabilità:
     - maggioranze di carte, denari e sette;
     - settebello e rebello;
     - scala di denari.
   - Così il settimo denaro vale poco se i denari sono già vinti, e il 3 di denari vale molto se si hanno già asso e 2.
   - Se la smazzata finisce dentro la ricerca, contano i punti esatti.
4. **Regole riscritte per la velocità** (`DealSearch`).
   - Uno stato compatto, senza allocazioni, circa 0,1 µs a posizione sul server.
   - Un test confronta ogni valore con la stessa ricerca giocata con il motore vero, su smazzate intere di Spincio e
     Spincione. Il test fallisce se si rompe una regola (provato con la gemella e con l'asso all'ultima giocata).
5. **Budget di calcolo deterministico** (niente orologio: le partite tra bot restano riproducibili).
   - Al massimo un certo numero di posizioni per decisione. Se un mondo costa molto, se ne esaminano meno (minimo 4).
   - Al massimo 12 giocate di profondità. Conta nella prima mano dello Spincione, che ne ha 16.
   - Livelli:

     | Livello | Mondi | Posizioni per decisione |
     |---|---|---|
     | "Normale" | 8 | 60.000 |
     | "Difficile" | 16 | 150.000 |
     | Server | 16 | 150.000 |
6. **Opzioni salvate invariate.** "Normale" resta `BotLevel.Greedy` e "Difficile" `BotLevel.Pimc`, quindi opzioni e partite
   salvate restano valide. Il server usa il nuovo `BotLevel.Lookahead`. L1 e L2 restano: L1 di riserva e come riferimento
   nei test, L2 per i confronti.

## Risultati
Simulatore, squadre contro squadre, lati alternati.

| Confronto | Spincio | Spincione |
|---|---|---|
| Nuovo "Normale" contro vecchio "Normale" (L2, 4 mondi) | **62,2%** (2000 partite) | **62,8%** (600) |
| Nuovo "Difficile" contro vecchio "Difficile" (L2, 12 mondi) | **64,1%** (1500) | **63,8%** (600) |
| Nuovo "Difficile" contro nuovo "Normale" | 53,7% (2000) | — |
| Nuovo "Normale" contro L1 | 62,7% (2000) | — |

**Tempo di decisione nel browser** (Chromium desktop, `cpu-time.mjs`; prima: mediana 30 ms, massimo 0,27–0,39 s):

| Modalità e livello | Mediana | p90 | Massimo |
|---|---|---|---|
| Spincio, "Normale" | 30 ms | 44 ms | 152 ms |
| Spincio, "Difficile" | 30 ms | 64 ms | 202 ms |
| Spincione, "Normale" | 30 ms | 51 ms | 338 ms |
| Spincione, "Difficile" | 32 ms | 67 ms | 312 ms |

Sul server (.NET nativo) una decisione costa in media 2 ms.

## Alternative provate e scartate
I primi quattro confronti sono stati fatti con il primo prototipo, che contava le carte con i pesi fissi di L1.

- **Fare sempre lo spazzino quando si può:** **39,6%** contro L3 (800 partite).
  - Quasi tutti gli spazzini "mancati" sono con l'asso. L3 lo tiene per prendere una tavola più ricca più avanti.
  - Prendere subito una sola carta con l'asso spreca la carta più forte del mazzo.
- **Aggiungere la valutazione di L1 alla media, come in L2:** 50,0%, nessun effetto.
- **Contare le carte prese con i pesi fissi di L1:** 45,4% contro i punti attesi (2000 partite).
- **Profondità** (contro una giocata oltre la fine della distribuzione):
  - fermarsi a fine distribuzione: 48,4%;
  - cercare due giocate oltre la fine: 49,6%;
  - limite a 12 giocate e budget: 50,5% nello Spincione e 52,1% nello Spincio, contro la ricerca senza limiti. Gli
    unici effetti sono la velocità e un tempo stabile.
- **Mondi:**
  - 16 contro 8: 55,0%;
  - 32 contro 16: 52,9%;
  - 4 contro 8: 47,4%.
  - Più mondi aiutano ancora un po'. Il limite è il telefono.

## Conseguenze
- (+) Tutte le CPU, compagno compreso, giocano molto meglio, a parità di tempo nel browser.
- (+) Il compagno ragiona sulle carte non uscite e su che cosa possono rispondere gli avversari, come chiesto.
- (−) Alcune scelte possono sembrare strane a prima vista, anche se fanno vincere di più:
  - il compagno tiene l'asso invece di fare subito uno spazzino da una carta;
  - accetta un piccolo rischio di spazzino se l'alternativa costa di più.

  Il giudice del simulatore, che guarda solo la mossa dopo, segna più "spazzini lasciati" (5,5% delle scelte contro 3,2%)
  e più "spazzini mancati" (13% contro 0,1%). Il risultato a fine partita è che L3 vince il 62–64%.
- (−) In ogni mondo L3 suppone che tutti vedano tutte le carte di quel mondo (limite noto di questo tipo di ricerca). La
  media su molti mondi lo attenua. Un passo successivo possibile: una ricerca sugli insiemi di informazione (ISMCTS).
