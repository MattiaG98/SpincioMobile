# ADR 0012 — Versione dell'app (versionamento semantico)

- **Stato:** Accettato (07/10/2026) — richiesta del proprietario ("iniziamo a versionare il progetto", partendo da 1.0.0)

## Contesto
Finora solo le regole avevano una versione (SPEC v1.4, `LocalGameSession.RulesVersion`). Per sapere quale build gira sul
telefono (la PWA può tenere in cache la versione precedente) serve anche una versione dell'app.

## Decisione
1. Una sola versione per tutta la solution: `<Version>` in `Directory.Build.props`, prima versione **1.0.0**.
   `IncludeSourceRevisionInInformationalVersion` è spento, così l'app mostra `1.0.0` e non `1.0.0+<commit>`.
2. Il client la legge da `AppInfo.Version` (attributo `AssemblyInformationalVersion`) e la mostra nel menu iniziale e nel
   menu di pausa.
3. Regole di incremento:
   - **MAJOR**: partite salvate non più riprendibili, oppure client e server online incompatibili;
   - **MINOR**: funzioni nuove o regole cambiate (con il loro `RulesVersion`);
   - **PATCH**: correzioni e ritocchi grafici.
4. Ogni PR che cambia l'app aggiorna `<Version>` e `CHANGELOG.md`. Dopo il merge su `main` si crea il tag `vX.Y.Z` sul
   commit di merge.
5. La versione delle regole resta separata: una regola cambiata aumenta `RulesVersion` **e** la versione dell'app.

## Conseguenze
- (+) Il proprietario vede subito quale versione ha installato.
- (−) Ogni PR deve ricordarsi di aumentare la versione (voce nella checklist del RUNBOOK).
