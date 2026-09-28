# ADR 0010 — Online v1: stanze in memoria, nessun hosting a pagamento

- **Stato:** Accettato (M7, 28/09/2026)
- **Integra:** ADR 0004

## Contesto
L'ADR 0004 prevedeva PostgreSQL + EF Core e un server pubblico. Il proprietario ha chiesto di evitare le fasi che richiedono pagamenti.

## Decisione
1. **Stanze in memoria** (`RoomRegistry`): nessun database. Le stanze inattive vengono rimosse dopo `IdleLifetime` (2 h).
2. **Server pronto ma non pubblicato.** Gira in locale (`dotnet run --project src/Spincio.Server`) e nei test end-to-end.
3. **Online nascosto nel client** finché `ServerUrl` in `wwwroot/appsettings.json` è vuoto. In Development punta al server locale.
4. Sono implementate le parti dell'ADR 0004 che non richiedono infrastruttura:
   - server autoritativo;
   - `expectedSequence`;
   - snapshot alla riconnessione;
   - CPU al posto del disconnesso dopo 30 s;
   - timeout di turno di 30 s;
   - commit-reveal del seed con verifica per replay nel client.
5. Codici stanza di 5 caratteri senza simboli ambigui. Token di posto casuali a 128 bit, conservati nel browser per rientrare.

## Conseguenze
- (+) Costo zero; tutta la logica online è testata (stanza con tempo finto + end-to-end SignalR).
- (−) Un riavvio del server chiude le partite in corso; niente storico partite né classifiche.
- (−) Per giocare online con amici fuori dalla rete locale serve scegliere un hosting: vedi `RUNBOOK.md`. Aggiungere un database resta possibile senza toccare il protocollo.
