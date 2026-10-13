# ADR 0015 — Giocatori online e inviti dall'app

- **Stato:** Accettato (13/10/2026, versione 1.7.0). Richiesta del proprietario: "una sezione giocatori online con la
  lista intera (tanto sono poche persone), per ognuno un pulsante per invitarlo in una nuova partita. L'invitato vede una
  notifica in alto e può accettare o rifiutare; se accetta sceglie se giocare con o contro chi lo ha invitato".
- **Integra:** ADR 0010 (stanze in memoria) e ADR 0014 (server temporaneo, indirizzo nel link di invito).

## Contesto
Con la 1.6.0 si gioca con gli amici mandando un link (WhatsApp). Il proprietario vuole anche invitare dall'app chi l'ha
già aperta, senza passare dai messaggi. Gli utenti sono pochi: basta la lista intera, senza ricerca né amicizie.

## Decisione
1. **Identità senza account**: ogni dispositivo ha un id casuale e il nome scelto dal giocatore
   (`localStorage["spincio.player"]`, `ProfileService`). Il nome si scrive una volta nella pagina Online.
2. **Lista dei giocatori online**: un secondo hub SignalR, `/hubs/lobby` (`LobbyHub`, `LobbyRegistry`), in memoria
   come le stanze.
   - L'app si collega appena conosce server e nome, **da qualunque schermata**: è in "Giocatori online" finché l'app è
     aperta. Più schede dello stesso dispositivo contano come un giocatore.
   - A ogni ingresso o uscita il server manda la lista a tutti.
   - Se il server non risponde l'app riprova da sola (ogni 20 secondi).
3. **Invito**: "Invita" accanto a un nome. Se non c'è ancora una stanza la crea; poi chiede al server di invitare.
   - Il server riconosce chi invita dal token segreto del suo posto, che resta sul server: l'invito porta solo il codice
     della stanza, il nome e il posto di chi invita, e le squadre ancora libere.
   - Un invito resta valido finché l'invitato risponde. Se il suo telefono era in background, lo riceve quando torna.
   - Un nuovo invito della stessa persona sostituisce il precedente.
4. **Notifica in alto** (`InviteBanner`, nel layout: c'è in ogni pagina, anche durante una partita contro la CPU):
   "📨 *Nome* ti invita a una partita online" con **Accetta** e **Rifiuta**.
   - Dopo **Accetta**: **Con *Nome*** (stessa squadra) e/o **Contro *Nome***, solo dove c'è ancora un posto. Un posto
     occupato dalla CPU conta come libero.
   - La scelta occupa un posto di quella squadra (`JoinRoomAs` sull'hub di gioco). Solo allora l'invito risulta
     accettato e l'app apre la stanza. Se la squadra si è riempita nel frattempo compare il motivo e si può scegliere
     l'altra.
   - Chi è già in una stanza o in una partita online può solo rifiutare.
   - Chi invita vede accanto al nome "Invitato…", "Ha accettato" o "Ha rifiutato". Avvia la partita come prima, con
     "Inizia la partita".
5. Il link di invito resta (ora "Invita con un link"), per chi non ha ancora aperto l'app con quel server. Lo stesso
   vale per il codice della stanza.

## Conseguenze
- (+) Invitare dall'app costa due tocchi; la scelta con/contro decide le squadre senza contare l'ordine di ingresso.
- (+) Nessun dato personale: id casuale e nome, solo in memoria sul server; spento il server, non resta nulla.
- (−) **Funziona solo con l'app aperta**: niente notifiche push. Le notifiche push sulla PWA richiedono
  l'installazione sulla Home (iOS 16.4+), un permesso esplicito e un servizio di invio; si può aggiungere più avanti.
- (−) Compare nella lista solo chi ha già usato quel server: un link di invito aperto una volta, oppure l'indirizzo
  scritto a mano. Con un codespace nuovo l'indirizzo cambia e va mandato di nuovo il link.
- (−) Il battito del server (ADR 0014) conta anche chi è nella lista: il codespace resta acceso finché qualcuno ha
  l'app aperta, non solo durante le partite. Un telefono in background di solito chiude la connessione.
- (−) Il nome non è verificato: due persone possono chiamarsi uguali. Con pochi utenti va bene.
- Verifica: test del registro (`LobbyTests`), test end-to-end con due client veri (`OnlineEndToEndTests`), test bUnit
  della notifica e della lista; nel browser `tools/browser-checks/lobby.mjs` con tre giocatori (rifiuto, accetto
  "contro", inizio della partita).
