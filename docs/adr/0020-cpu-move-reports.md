# ADR 0020 — Segnalazioni delle mosse della CPU: dal telefono a una issue che si può rigiocare

- **Stato:** Accettato (13/11/2026, versione 1.14.0). Dalla 1.14.1, su richiesta del proprietario, il pulsante è
  accanto al ritratto del compagno, al posto della voce nel menu di pausa (il resto è invariato).
- **Integra:** ADR 0003 (seed + mosse), ADR 0016 (server su Render), ADR 0019 (CPU L3)
- **Richiesta del proprietario:** una sezione in gioco per avvisare quando la CPU "sbaglia": l'elenco delle mosse del
  compagno, e per ognuna un box di testo con il ragionamento giusto, "così che l'app evolva il proprio modo di
  ragionare in base ai suggerimenti scritti dagli utenti".

## Contesto
- Una segnalazione serve solo se si può rivedere **esattamente** la situazione. Le partite sono già seed + mosse
  (ADR 0003), quindi bastano poche centinaia di byte per rigiocarla identica.
- Un testo libero non si traduce da solo in una CPU migliore, e i giocatori non hanno sempre ragione. Fare sempre subito
  lo spazzino con l'asso sembrava giusto, e invece faceva perdere il 20% delle partite in più (ADR 0019).
- L'app è su GitHub Pages: niente backend, a parte il server online gratuito su Render. Quel server dorme quando
  nessuno gioca e non ha un disco che sopravviva ai riavvii.

## Decisione
1. **Nel gioco** (partite contro la CPU), un pannello "Segnala una mossa di Titti" (`ReportPanel`).
   - **Dove si apre:** dal menu di pausa, dal riepilogo di fine smazzata e dalla schermata di fine partita, cioè nei
     momenti in cui si ripensa alle mosse.
   - **Passo 1, la mossa:** le mosse del compagno, dalla più recente. Ogni riga ha la carta, la presa e "smazzata ·
     mano". Toccata una riga, l'elenco si richiude su quella mossa ("Cambia mossa") e sotto compare la situazione: le
     carte sul tavolo e, se quella mano è finita, le carte del compagno. Quelle di una mano in corso restano nascoste.
   - **Passo 2, cosa non va:** motivi rapidi da toccare ("Doveva fare spazzino", "Ha lasciato lo spazzino", "Poteva
     prendere di meglio", "Doveva tenersi la carta", "Altro").
   - **Passo 3, la carta giusta** (facoltativa): si tocca fra le carte che il compagno aveva.
   - **Passo 4, il ragionamento:** testo libero, al massimo 500 caratteri, con un esempio.
   - **Il pulsante in fondo** è sempre visibile e dice il passo successivo ("Scegli una mossa" → "Dicci cosa non va" →
     "Invia segnalazione").
   - **Dopo l'invio:** "Grazie!", con "Segnala un'altra mossa" e "Torna alla partita".
2. **La segnalazione** (`CpuMoveReport`, Contracts) contiene:
   - versione dell'app e delle regole, modalità, livello;
   - seed e mosse fino a quella segnalata compresa;
   - motivi, carta suggerita e testo.

   Non contiene nome né identificativi del giocatore. Il pannello ricorda di non scrivere dati personali.
3. **Sul telefono** la segnalazione si salva subito (`localStorage["spincio.cpuReports"]`) e parte appena il server
   risponde: anche offline o con il server addormentato non si perde niente. Riprova a ogni apertura dell'app; scarta
   solo ciò che il server dichiara non valido.
4. **Sul server** `POST /api/cpu-reports`:
   - **Controllo** (`CpuMoveReports.Problem`): rigioca la partita con il motore. Ogni mossa deve essere legale, l'ultima
     una giocata di una CPU, la carta suggerita una che quella CPU aveva. Così non passa niente di inventato.
   - **Limite:** 10 segnalazioni all'ora per indirizzo.
   - **Destinazione:** una **issue GitHub** (`GitHubIssueSink`) in un repository scelto dal proprietario, meglio
     privato, con un token che può solo aprire issue lì. Repository e token sono impostazioni di Render, mai nel codice.
   - **Contenuto della issue:** il testo del giocatore (con `@` e `#` neutralizzati, così non avvisa persone e non
     chiude issue), tavola e mano della CPU, punteggio e il JSON da rigiocare.
   - **Senza destinazione configurata** risponde 503 e i telefoni tengono le segnalazioni.
5. **Analisi:**
   - `Spincio.Simulator --report file.json` rigioca la segnalazione e chiede a L3, con 64 mondi, il valore di ogni
     giocata possibile (`LookaheadBot.Explain`), segnando quella fatta e quella suggerita.
   - Una modifica alla CPU entra solo se vince nei tornei del simulatore (ADR 0019) e arriva ai giocatori con una nota
     in "Novità".

## Come "evolve" la CPU
I suggerimenti non cambiano la CPU da soli, ed è voluto:
- un testo libero va capito;
- un consiglio sbagliato peggiorerebbe la CPU per tutti.

Il ciclo è:
1. la segnalazione arriva;
2. si rigioca la posizione;
3. si capisce se e perché la CPU sbagliava;
4. si prova una correzione su migliaia di partite;
5. la correzione esce nella versione successiva.

Le segnalazioni con la "carta giusta" sono anche esempi verificabili: in futuro potranno formare una raccolta di
posizioni su cui misurare quanto spesso la CPU è d'accordo con i giocatori.

**Possibile automazione successiva:** un'attività programmata (per esempio settimanale) in cui Claude legge le nuove
issue, le rigioca e apre una PR solo se una correzione vince nei tornei. Da decidere con il proprietario: ha un costo e
va sorvegliata.

## Alternative scartate
- **Issue direttamente dall'app:** il token finirebbe nel codice pubblico della PWA.
- **Repository pubblico dell'app:** i testi dei giocatori sarebbero pubblici.
- **Condividere la segnalazione via WhatsApp o mail:** nessuna raccolta centrale, nessun controllo, dati sparsi.
- **Server con un suo archivio:** il piano gratuito di Render non ha un disco che sopravviva ai riavvii.
- **Apprendimento automatico dai testi:** non affidabile e non misurabile; vedi sopra.

## Conseguenze
- (+) Ogni segnalazione è una posizione esatta e rigiocabile, raccolta in un solo posto, senza dati personali.
- (+) Funziona offline e con il server addormentato.
- (−) Serve un'attivazione una tantum del proprietario (repository privato, token, due impostazioni su Render; vedi
  RUNBOOK). Il token scade e va rinnovato.
- (−) Per ora solo le mosse del compagno nelle partite contro la CPU. Online le mosse si conoscono tutte solo a fine
  partita; le mosse degli avversari CPU sono un'estensione semplice.
