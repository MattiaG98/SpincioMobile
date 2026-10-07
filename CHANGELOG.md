# Changelog

Versioni dell'app Spincio ([versionamento semantico](https://semver.org/lang/it/), ADR 0012). La versione è in
`Directory.Build.props` (`<Version>`) e si vede nel menu iniziale e nel menu di pausa. Le regole hanno una versione propria
(`docs/SPEC.md`, "regole v1.4" nel menu di pausa).

## 1.0.0 — 07/10/2026
Prima versione numerata: tutto ciò che è stato rilasciato finora (M0–M12) più:
- versione dell'app visibile nel menu iniziale e nel menu di pausa;
- niente scorrimento della pagina: lo schermo di gioco occupa sempre lo schermo intero; scorrono solo le regole, lo storico
  delle mosse e le pagine più alte dello schermo;
- la tavola non si allarga più: ha la misura di inizio partita e le carte si rimpiccioliscono per starci (provato fino a 16 carte);
- animazione: la carta giocata si rimpicciolisce mentre si avvicina alla tavola, fino alla misura delle carte in tavola;
- il nome del mazziere va su una riga a parte e le dichiarazioni dei giocatori non spostano più la tavola.
