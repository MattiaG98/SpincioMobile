# ADR 0013 — Entrambi i livelli della CPU usano la ricerca (L2)

- **Stato:** Accettato (08/10/2026, versione 1.2.0) — richiesta del proprietario: "migliora il livello di intelligenza artificiale dei bot"
- **Modifica:** ADR 0005 (L1 come avversario dell'MVP)

## Contesto
"Normale" usava L1 (valutazione a una mossa), "Difficile" L2 (PIMC) con 8 mondi. Misure fatte per questa decisione
(`Spincio.Simulator`, partite tra squadre, lati alternati):

- **L1 non migliora cambiando l'euristica.** Rischio di presa dell'avversario con la regola dell'asso (P8), maggioranze già
  decise e valore dinamico dello spincio: cambiano il 9,5% delle scelte di L1 ma vincono il 50–51% contro la L1 di prima
  (8000–12000 partite per configurazione, anche dopo una taratura dei pesi). Usati come politica di L2: 49,5% (400 partite).
  Con 3 carte in mano e prese obbligatorie le scelte vere sono poche.
- **La ricerca sì.** L2 con 4 mondi batte L1 nel 57,8% (800 partite); più mondi aiutano poco (16 contro 8: 52,2% su 600).
- **Nel browser** (Chromium desktop, `cpu-time.mjs`): 4 mondi → decisione mediana 29–32 ms, massimo 0,27 s; 8 mondi → mediana 30 ms, massimo 0,3 s; 12 mondi → mediana 33 ms, massimo 0,39 s.

## Decisione
1. "Normale" = L2 con **4 mondi**; "Difficile" = L2 con **12 mondi**. Valgono per tutte e tre le CPU, compagno compreso.
   Nelle opzioni salvate "Normale" resta `BotLevel.Greedy`: opzioni e partite salvate restano valide (una partita salvata
   rigioca i comandi, qualunque sia il bot).
2. L2 legge anche i **silenzi**: chi ha giocato la prima carta della distribuzione senza accusare non aveva niente da accusare
   (A3), e i mondi che gli darebbero una mano da accuso vengono ricampionati. Effetto misurato: 51,5% (1000 partite, non
   significativo); tenuto perché usa un'informazione vera a costo quasi nullo.
3. Il server online usa L2 (16 mondi, .NET nativo) per i posti vuoti.
4. L1 resta com'era: è la politica delle simulazioni di L2 e il bot di riferimento nei test.

## Risultati (1000 partite ciascuno, contro i livelli della 1.1.0)
- "Normale" nuovo contro "Normale" vecchio: **59,7%**.
- "Difficile" nuovo contro "Difficile" vecchio: **53,1%**.

## Conseguenze
- (+) CPU più forti a entrambi i livelli; i bot accusano sempre quando possono (verificato su 10 partite intere, test
  `Bots_declare_every_time_they_can_over_whole_matches`).
- (−) Manca un livello "facile". Se serve, si aggiunge un terzo livello con L1.
- (−) Più calcolo sul telefono; da ricontrollare su un iPhone vecchio con `cpu-time.mjs` se le CPU sembrano lente.
