# Changelog

Versioni dell'app Spincio ([versionamento semantico](https://semver.org/lang/it/), ADR 0012). La versione è in
`Directory.Build.props` (`<Version>`) e si vede nel menu iniziale e nel menu di pausa. Le regole hanno una versione propria
(`docs/SPEC.md`, "regole v1.4" nel menu di pausa).

## 1.7.1 — 13/10/2026
- **Server fisso su Render, preparato** (ADR 0016): il server online si può pubblicare gratis su Render con un clic
  (`render.yaml`, immagine Docker del server). Quando il proprietario lo attiva, l'app lo userà da sola (versione
  successiva).
- Mentre l'app si collega, la pagina Online avvisa che un server spento può metterci fino a un minuto a svegliarsi.

## 1.7.0 — 13/10/2026
- **Giocatori online e inviti dall'app** (ADR 0015):
  - nella pagina Online c'è **"Giocatori online"**: tutti quelli che hanno l'app aperta sullo stesso server, ognuno con
    il pulsante **Invita** (se serve crea la stanza);
  - chi viene invitato vede **in alto, in qualunque schermata**, "📨 *Nome* ti invita a una partita online" con
    **Accetta** e **Rifiuta**; dopo Accetta sceglie **Con *Nome*** o **Contro *Nome*** (solo dove c'è posto) ed entra
    nella stanza;
  - chi invita vede accanto al nome "Invitato…", "Ha accettato" o "Ha rifiutato";
  - il nome si scrive una volta e l'app lo ricorda; il link di invito resta ("Invita con un link").
- Corretto: dopo aver lasciato una partita online, entrare in una nuova stanza riapriva la partita finita.

## 1.6.0 — 13/10/2026
- **Gioca online con gli amici, con un server temporaneo gratuito** (ADR 0014):
  - **"Gioca online con gli amici"** è sempre nel menu;
  - l'app prende l'indirizzo del server dal **link di invito** (oppure da un campo) e lo ricorda;
  - nella stanza, **"Invita gli amici"** apre la condivisione del telefono (WhatsApp, Messaggi…) con un link che
    porta già al server e alla stanza;
  - il server si accende con un clic su **GitHub Codespaces** (gratis con l'account GitHub) e stampa il link da aprire.
    Resta acceso finché si gioca e poi si spegne da solo. Istruzioni passo passo in `docs/RUNBOOK.md`.
- **Punti più piccoli e più rapidi**: la scritta dei punti è più piccola e dura circa metà (0,8 s invece di 1,2 s).

## 1.5.4 — 11/10/2026
- **Punti insieme alla mossa**: la scritta dei punti di uno spazzino o di una Mariana parte insieme alla carta, non più
  dopo. Esce dal giocatore che fa la mossa (non dal centro della tavola, dove coprirebbe la presa) e vola sul punteggio
  mentre la carta va in tavola; il numero cambia sempre solo quando i punti sono arrivati. Gli accusi e i punti di fine
  smazzata restano come prima (sono momenti a sé).

## 1.5.3 — 11/10/2026
- **Accuso**: dopo aver premuto "Accusa" accanto a "Tocca a te" non resta più nulla. Prima restava fino a fine smazzata
  un'etichetta dorata con l'ultimo accuso, identica al pulsante. L'accuso si vede nei punti che volano sul punteggio e
  nello storico ("Tu accusi: …").
- Il pulsante è più compatto ("Accusa +3" con il tipo sotto, in piccolo) e non esce più dallo schermo con gli accusi
  dal nome lungo; se serve, si accorcia il testo accanto, mai il pulsante.
- Le etichette "Accuso +N" sotto le CPU valgono solo per la distribuzione in corso (prima restavano per tutta la smazzata).

## 1.5.2 — 11/10/2026
- **Avatar** (scelte del proprietario): tutti e tre i personaggi sono uomini con i capelli rossi ricci. **Titti** e
  **Vava** hanno la carnagione chiara, **Tito** è di colore. Titti è il giovane allegro con le lentiggini, Tito ha
  coppola, baffi rossi e occhiolino, Vava ha barba rossa riccia e occhiali tondi.

## 1.5.1 — 09/10/2026
- **Avatar**: tutti e tre i personaggi hanno i capelli rossi ricci (richiesta del proprietario). Titti con una nuvola di
  ricci fino alle spalle, Tito con i ricci che spuntano dalla coppola e baffi rossi, Vava con i ricci raccolti in uno
  chignon. Il resto dei tratti resta: orecchini, coppola e occhiolino, occhiali e perle.

## 1.5.0 — 09/10/2026
- **Avatar delle CPU**: ogni giocatore ha il suo ritratto accanto al nome, disegnato apposta per Spincio. **Titti**
  (compagna: allegra, coda di cavallo), **Tito** (avversario: coppola, baffi, l'occhiolino del furbo) e **Vava**
  (avversaria: chignon grigio e occhiali, la saggia del tavolo). Bordo oro per il compagno, rosso per gli avversari;
  quando tocca a loro "ci pensano" ondeggiando. Nel gioco online al posto del ritratto c'è l'iniziale del nome.

## 1.4.0 — 09/10/2026
- **Contributi**: nuovo pannello dal menu iniziale con chi ha contribuito a Spincio (Megako, Nandone) e la provenienza
  delle carte. Nel menu, Regole, Statistiche, Opzioni e Contributi sono ora su una griglia 2×2: il menu resta senza
  scorrimento anche su iPhone SE.

## 1.3.0 — 09/10/2026
- **Mariana**: quando un giocatore prende un 5 e un 3 (di qualunque seme) con un fante compare la scritta **MARIANA**.
  Non dà punti. Se la presa svuota la tavola compare MARIANA al posto di "Spazzino!" e lo spazzino vale comunque 1 punto.
  Anche nello storico delle mosse ("— MARIANA!") e nella pagina Regole. Regolamento: v1.4 con la nota "scritte" e AT-40;
  nessuna regola cambiata, quindi le partite salvate si riprendono.
- Online (server): all'inizio della partita l'impegno anti-imbroglio arriva prima delle carte (prima arrivava subito dopo).

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
