# ADR 0017 — Partite che sopravvivono a un riavvio del server (punto di ripristino nei client)

- **Stato:** Accettato (15/10/2026, versione 1.8.0). Proposta in ADR 0016, approvata dal proprietario ("Sì").
- **Integra:** ADR 0003 (salvataggio = seed + mosse), ADR 0004 (server autoritativo, impegno e rivelazione), ADR 0010
  (stanze in memoria), ADR 0016 (Render).

## Contesto
Le stanze vivono nella memoria del server. Su Render (e in un codespace) il server si riavvia a ogni aggiornamento,
per le manutenzioni, o si spegne quando nessuno è collegato da 15 minuti. Ogni volta le partite in corso andavano
perse. Un database gratuito aggiungerebbe un account, un servizio da tenere vivo e dati da gestire.

Una partita però si ricostruisce da pochissime cose: seed e mosse (ADR 0003), sale per la verifica finale, nomi dei
posti e i token segreti dei giocatori. I giocatori sono sempre collegati quando la partita va avanti, quindi possono
custodire queste informazioni, purché non le possano leggere: il seed rivela le carte di tutti.

## Decisione
1. **Punto di ripristino sigillato.** A ogni mossa il server prepara un `RoomCheckpoint` (codice stanza, seed, sale,
   posti con nome, token e CPU, elenco delle mosse) e lo sigilla:
   - JSON compresso con Brotli;
   - cifrato e autenticato con **AES-GCM**, con una chiave ricavata (SHA-256) dal segreto
     `Rooms:CheckpointKey`.

   Chi lo tiene non può leggerlo né modificarlo. Viaggia in `GameUpdate.Checkpoint` a ogni aggiornamento: pochi KB, al
   massimo una dozzina per una partita intera.
2. **Il client lo custodisce** in `localStorage["spincio.onlineCheckpoint"]`, accanto al token del posto. Si cancella
   quando si entra in un'altra stanza o si lascia la partita.
3. **Dopo un riavvio** il client prova `Rejoin(token)`. Se il server non conosce più la partita, chiama
   `Resume(token, checkpoint)`:
   - il server controlla il sigillo e che il token sia uno dei giocatori;
   - ricostruisce la stanza con lo stesso codice, rigiocando le mosse con il motore (ADR 0003);
   - registra i token di tutti, così chi torna dopo ritrova la stessa stanza;
   - chi non torna viene sostituito da una CPU dopo la solita attesa;
   - la prima mossa dopo la ricostruzione aspetta almeno `RestoreSettle` (3 s), per dare tempo agli altri di
     rientrare.
4. **Il più aggiornato vince.** Se chi rientra dopo ha un punto di ripristino con più mosse della stessa partita, e
   nel frattempo non si è giocato altro, le mosse mancanti vengono aggiunte (`ExtendAsync`). Mai all'indietro.
5. **La verifica resta valida.** L'impegno è ricalcolato dagli stessi seed e sale, e a fine partita la rivelazione
   corrisponde all'impegno pubblicato all'inizio (test `CheckpointTests`).
6. **Riconnessione insistente.** Se la riconnessione automatica di SignalR si arrende (dopo circa 42 s), l'app
   riprova ogni 5 secondi per 3 minuti mentre la partita è in corso. Un server che si sveglia ci mette fino a un
   minuto.
7. **La chiave:**
   - su Render la genera Render una volta per tutte (`render.yaml`, `generateValue`) e resta uguale a ogni deploy;
   - nel codespace lo script la crea e la conserva in `~/.spincio-checkpoint-key`;
   - senza chiave il server ne usa una casuale: i punti di ripristino valgono solo finché il processo resta acceso.

## Conseguenze
- (+) Un aggiornamento del server, una manutenzione o un risveglio non fanno più perdere la partita. Niente database,
  niente account in più, costo zero.
- (+) Nessuna informazione nascosta esce in chiaro: il punto di ripristino è opaco per i client.
- (−) Chi rientra per primo con un punto di ripristino vecchio può far ripartire la partita da qualche mossa prima,
  se gli altri non rientrano entro i 3 secondi di attesa. Nella pratica i telefoni collegati hanno tutti l'ultimo.
- (−) Chi ha la chiave del server potrebbe leggere i punti di ripristino. Per questo vive solo nelle impostazioni del
  servizio e non nel repository.
- (−) Le stanze non ancora iniziate non si ricostruiscono: dopo un riavvio si crea una stanza nuova.
- Verifica:
  - `CheckpointTests`: ricostruzione identica e partita portata fino alla fine con la verifica che regge; sigillo
    cambiato, chiave diversa o non giocatore rifiutati; avanzamento con un punto di ripristino più recente;
  - `OnlineEndToEndTests.A_match_survives_a_server_restart`: il client vero, un'istanza del server spenta e una
    nuova accesa; l'app si ricollega da sola, ritrova la stessa partita e la continua.
