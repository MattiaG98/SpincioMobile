# Changelog

Versioni dell'app Spincio ([versionamento semantico](https://semver.org/lang/it/), ADR 0012). La versione è in
`Directory.Build.props` (`<Version>`) e si vede nel menu iniziale e nel menu di pausa. Le regole hanno una versione propria
(`docs/SPEC.md`, "regole v1.4" nel menu di pausa).

## 1.3.0 — 09/10/2026
- **Mariana**: quando un giocatore prende un 5 e un 3 (di qualunque seme) con un fante compare la scritta **MARIANA**.
  Non dà punti. Se la presa svuota la tavola compare MARIANA al posto di "Spazzino!" e lo spazzino vale comunque 1 punto.
  Anche nello storico delle mosse ("— MARIANA!") e nella pagina Regole. Regolamento: v1.4 con la nota "scritte" e AT-40;
  nessuna regola cambiata, quindi le partite salvate si riprendono.

## 1.2.1 — 09/10/2026
- **Aggiornamenti visibili**: l'app installata controlla se c'è una versione nuova ogni volta che torna in primo piano
  (su iPhone l'app viene ripresa senza ricaricarsi, quindi prima non controllava quasi mai). Quando la versione nuova è
  scaricata compare in alto "È pronta una nuova versione di Spincio" con il pulsante **Aggiorna**: un tocco e l'app si
  ricarica con la versione nuova. La partita in corso resta salvata (si riprende dal menu).

## 1.2.0 — 08/10/2026
- **Statistiche** (dal menu iniziale e dal menu di pausa): partite giocate, vinte e percentuale; serie di vittorie attuale e
  migliore; abbandonate; risultati contro CPU normale, CPU difficile e online; smazzate, punti della squadra (con la media),
  spazzini e accusi fatti da te, settebelli, rebelli, smazzate con lo spincio, vittorie con tutti i denari, vittoria più
  larga. Salvate sul dispositivo, con "Azzera statistiche". Contano solo le mosse giocate dal vivo: riprendere una partita
  salvata non conta due volte.
- **CPU più forti** (ADR 0013): "Normale" ora simula le carte nascoste come "Difficile" (4 mondi) e vince il 59,7% delle
  partite contro il "Normale" di prima; "Difficile" prova 12 mondi invece di 8 e tiene conto anche di chi non ha accusato
  (53,1% contro il "Difficile" di prima). Anche le CPU del gioco online usano la ricerca.
- **Accusi delle CPU**: già prima le CPU accusavano sempre quando potevano (verificato su partite intere, ora c'è un test);
  il badge sotto il nome ora è compatto ("Accuso +2") e non va più a capo su quattro righe.

## 1.1.0 — 08/10/2026
- Animazione dei punti: quando una squadra fa punti, "+N" compare dove sono stati fatti e vola sul punteggio della squadra,
  che si illumina all'arrivo; il numero cambia solo quando i punti sono arrivati. Oro per noi, rosso per loro.
  - **Spazzino**: "Spazzino! +1" sulla tavola (sostituisce la scritta "Spazzino!" di prima; resta quella se le animazioni sono spente).
  - **Accuso**: dal posto di chi accusa, con il nome dell'accuso (es. "Tris +7").
  - **Fine smazzata**: i punti di fine smazzata volano sul punteggio quando si chiude il riepilogo; finché il riepilogo è aperto
    il punteggio in alto resta quello di prima.
- Segue la velocità scelta in Opzioni; con le animazioni spente i punti si aggiornano subito come prima.

## 1.0.0 — 07/10/2026
Prima versione numerata: tutto ciò che è stato rilasciato finora (M0–M12) più:
- versione dell'app visibile nel menu iniziale e nel menu di pausa;
- niente scorrimento della pagina: lo schermo di gioco occupa sempre lo schermo intero; scorrono solo le regole, lo storico
  delle mosse e le pagine più alte dello schermo;
- la tavola non si allarga più: ha la misura di inizio partita e le carte si rimpiccioliscono per starci (provato fino a 16 carte);
- animazione: la carta giocata si rimpicciolisce mentre si avvicina alla tavola, fino alla misura delle carte in tavola;
- il nome del mazziere va su una riga a parte e le dichiarazioni dei giocatori non spostano più la tavola.
