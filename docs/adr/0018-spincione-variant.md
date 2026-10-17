# ADR 0018 — Modalità Spincione: una variante del motore, non un secondo motore

- **Stato:** Accettato (17/10/2026, versione 1.9.0, regolamento v1.5). Regole dettate dal proprietario (SPEC §1.7).
- **Integra:** ADR 0003 (seed + mosse), ADR 0004 (impegno e verifica), ADR 0008 (stati ipotetici), ADR 0017 (punti di
  ripristino).

## Contesto
Il proprietario ha chiesto la modalità Spincione:
- due mazzi;
- prima mano da 4 carte;
- accusi raddoppiati in prima mano;
- accusi nuovi (colore, scala);
- punto per la presa dello stesso seme;
- settebello e rebello per copia;
- niente vittoria immediata con i denari.

Il "bàgher" del backlog non esiste: era un nome sbagliato.

Le differenze con lo Spincio sono poche e quasi tutte numeriche. La più profonda è che con due mazzi **la stessa carta
esiste due volte**: il motore, i bot e l'interfaccia davano per scontato che ogni carta fosse unica.

## Decisione
1. **Un solo motore con una variante**: `GameVariant { Spincio, Spincione }`.
   - Fa parte della partita, come il seed (`NewMatch(seed, dealer, variant)`, `MatchState.Variant`).
   - Le differenze stanno in `VariantRules`: mazzo, carte per mano, giocate per smazzata, interruttori.
   - La valutazione degli accusi dello Spincione ha una sua funzione; quella dello Spincio resta intatta.
2. **Lo Spincio non deve cambiare di un bit.** Il test AT-50 confronta l'impronta di 20 partite intere con quella
   registrata con il motore della 1.8.1. Anche bot e simulatore danno gli stessi risultati di prima con lo stesso seed.
3. **Carte identiche:**
   - le mosse e le prese uguali compaiono una volta sola;
   - la memoria dei bot conta le copie;
   - gli stati ipotetici controllano che ogni carta compaia il numero giusto di volte;
   - l'interfaccia dà chiavi distinte alle copie (`7D`, `7D#2`) e le animazioni scelgono elementi distinti.
4. **Tutto ciò che rigioca una partita conosce la variante:** salvataggi, verifica dell'impegno (`MatchReveal.Variant`),
   punti di ripristino, stanze online (`CreateRoomFor`, `RoomInfo.Variant`), inviti.
5. **La scelta della modalità** sta nel menu iniziale e nella pagina Online, ed è ricordata con le opzioni.
6. Le CPU sono le stesse, con un peso in più per la presa dello stesso seme (vale come uno spazzino). Misure:
   - L1 contro L0: 96,4%;
   - L2 contro L1: 51–53%. Le partite durano circa 2 smazzate, quindi la fortuna pesa di più;
   - tempo per decisione circa 1,4 volte lo Spincio (3,7 ms con 4 mondi, 6,1 ms con 12, in .NET).

## Conseguenze
- (+) Una sola implementazione delle regole comuni: le correzioni valgono per entrambe le modalità.
- (+) Online, salvataggi, verifica e ripristino funzionano per lo Spincione senza codice dedicato.
- (−) Ogni nuova regola va scritta pensando alle copie delle carte, e chi sviluppa deve ricordarsene. I property test
  con lo Spincione (conservazione delle 80 carte, mosse legali tutte diverse) lo controllano.
- (−) A L2 lo Spincione dà meno vantaggio che allo Spincio. Si potrà tarare a parte se il proprietario lo trova troppo
  facile.
