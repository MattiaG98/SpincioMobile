# Spincio — Specifica (Regolamento v1.7)

> **Fonte di verità.** Queste sono le regole di casa dello Spincio (variante dello Spazzino). In caso di conflitto con qualunque altra fonte, vale questo documento.
> Ogni modifica alle regole richiede: aggiornamento di questo file, nuova versione del regolamento, test `AT-xx` corrispondenti.

## Changelog

| Versione | Data | Modifica |
|---|---|---|
| v1.7 | 22/10/2026 | **Asso all'ultima giocata** (dettata dal proprietario, Spincio e Spincione): l'asso che all'ultima giocata svuota la tavola fa spazzino (+1), sia prendendo tutta la tavola (P8) sia prendendo l'unico asso in tavola. Le altre carte all'ultima giocata restano senza spazzino (P6). AT-37 cambiato, AT-52 nuovo. |
| v1.6 | 17/10/2026 | **Gemella** nello Spincione (SP10), dettata dal proprietario: la carta che prende la sua gemella vale **2 punti** al posto del punto dello stesso seme; se svuota la tavola si aggiunge lo spazzino (**3**, "gemella con cappella"); vale anche all'ultima giocata. AT-43 senza il caso della gemella, AT-51 nuovo. Lo Spincio non cambia. |
| v1.5 | 17/10/2026 | **Nuova modalità Spincione** (§1.7), dettata dal proprietario: due mazzi, prima distribuzione da 4 carte, accusi in prima mano raddoppiati, accusi nuovi (**colore**, **scala**), punto in più per la **presa dello stesso seme**, settebello e rebello contati per copia, niente vittoria immediata con i denari. Il "bàgher" del backlog non esiste: il proprietario l'aveva chiamato così per errore. Lo Spincio non cambia. AT-41…AT-50. |
| v1.1 | 24/09/2026 | Regolamento confermato (Fasi 0–3) |
| v1.4 (terminologia) | 07/10/2026 | La scala di denari si chiama **spincio** (prima "napola"), su indicazione del proprietario. Nessuna regola cambiata. |
| v1.4 (scritte) | 09/10/2026 | **Mariana** (dettata dal proprietario): quando un fante prende un 5 e un 3 compare la scritta MARIANA; se la presa svuota la tavola sostituisce la scritta dello spazzino, che vale comunque 1 punto. Solo una scritta: nessuna regola e nessun punto cambiati. AT-40. |
| v1.4 | 06/10/2026 | **P8 precisata dal proprietario:** se in tavola c'è già un asso, l'asso giocato prende solo quell'asso (presa normale, P2); se così svuota la tavola è spazzino come ogni presa. AT-34 e AT-38 aggiornati, AT-39 nuovo. |
| v1.3 | 05/10/2026 | **Nuova regola P8 (dettata dal proprietario): asso pigliatutto.** L'asso giocato con carte in tavola prende tutta la tavola ed è spazzino (tranne all'ultima giocata, P6); a tavola vuota si cala. AT-33…AT-38. |
| v1.2 | 25/09/2026 | **Chiarimento F7** (confermato dal proprietario): vale anche nelle partite di spareggio. **Correzione di una deduzione** in §5 (invariante sulle carte di pari valore). `NewMatch` restituisce una `Transition`. Nessuna regola di gioco cambiata. |

## 0. Notazione

Carte nei test: rango `A,2,3,4,5,6,7,J,N,K` (J = fante, N = cavallo, K = re) + seme `D,C,S,B` (Denari, Coppe, Spade, Bastoni).
Esempi: `KD` = re di denari (**rebello**), `7D` = **settebello**.

## 1. Regolamento

### 1.1 Setup
| ID | Regola |
|---|---|
| S1 | Mazzo da briscola da 40 carte, grafica **piacentina**. Valori: asso–7 = 1–7, fante 8, cavallo 9, re 10. |
| S2 | **2v2**: i compagni siedono uno di fronte all'altro; si gioca in senso **antiorario**. Squadra A = posti 0 e 2, squadra B = posti 1 e 3. |
| S3 | Il primo mazziere è casuale; alla smazzata successiva passa al giocatore seguente in senso antiorario. |
| S4 | Prima distribuzione: 3 carte a testa + 4 scoperte in tavola. Poi 3 a testa quando le mani sono vuote: 3 distribuzioni e 36 giocate per smazzata. |
| S5 | Se in tavola ci sono **almeno 2 assi**, si ridistribuisce: si rimescola tutto (mani comprese) e si ridistribuisce, stesso mazziere, RNG deterministico. |
| S6 | Inizia il giocatore dopo il mazziere. |

### 1.2 Prese
| ID | Regola |
|---|---|
| P1 | Si prende per **uguaglianza** di valore oppure per **somma** di 2 o più carte. **Anche le figure** prendono per somma (es. K prende 7+3; K prende J+2). |
| P2 | Se in tavola c'è una carta di valore uguale, bisogna prendere quella: la somma è vietata. Con più carte uguali se ne prende **una sola**, a scelta. |
| P3 | Tra più prese per somma possibili **sceglie il giocatore**. |
| P4 | **Obbligo di presa sulla carta giocata:** si può giocare qualunque carta; se quella carta può prendere, deve prendere. Si può quindi calare una carta che non prende anche se un'altra carta in mano prenderebbe. |
| P5 | Nessuna presa doppia (uguaglianza + somma) nella stessa giocata. |
| P6 | **Spazzino** (la presa svuota la tavola) = **1 punto**. **Non vale** con l'ultima giocata della smazzata, **tranne con l'asso** (dalla v1.7): l'asso che all'ultima giocata svuota la tavola fa spazzino, sia prendendo tutta la tavola (P8) sia prendendo l'unico asso in tavola. |
| P7 | Le carte rimaste in tavola a fine smazzata vanno alla squadra che ha preso per ultima; non è spazzino. Se nessuno ha preso in tutta la smazzata, non vanno a nessuno. |
| P8 | **Asso pigliatutto.** L'asso giocato quando in tavola ci sono carte **prende tutta la tavola**, ed è **spazzino** (1 punto), anche all'ultima giocata (P6, dalla v1.7). **Eccezione:** se in tavola c'è già un asso, l'asso giocato prende **solo quell'asso** (presa normale per uguaglianza, P2); se così svuota la tavola è spazzino come ogni presa. A **tavola vuota** l'asso si cala, senza punti. |

### 1.3 Accusi (punti della mano)
| ID | Regola |
|---|---|
| A1 | A **ogni distribuzione**: somma dei valori della mano **≤ 9** = 2 punti, oppure **3** se ci sono esattamente due carte dello stesso rango (coppia). **Tris** = 7 punti. Si cumulano: 3-3-3 = 7+2 = 9 (non 7+3). |
| A2 | I punti vanno alla **squadra**. |
| A3 | Si dichiarano con un **pulsante**, attivo solo se la mano vale punti. Si può dichiarare **solo nel proprio turno, prima di giocare la propria prima carta** di quella distribuzione; se non si dichiara, i punti sono persi. |
| A4 | Gli altri vedono **solo tipo e punti** dell'accuso, mai le carte. |

### 1.4 Fine smazzata (1 punto ciascuno; in caso di pari, 0)
| ID | Regola |
|---|---|
| F1 | Più carte. |
| F2 | Più denari. |
| F3 | Rebello (`KD`). |
| F4 | Settebello (`7D`). |
| F5 | Primiera = **più 7**. |
| F6 | **Spincio** (scala di denari; altrove detta "napola"): scala consecutiva che parte dall'asso (A,2,3,4,5,6,7,J,N,K), lunga almeno 3 carte; vale quanto la sua lunghezza (A–5 = 5). |
| F7 | **Tutti i 10 denari** presi = **vittoria immediata** della partita, qualunque sia il punteggio. Vale anche nelle partite di spareggio. |
| — | Tutti i punti si **cumulano** (il `7D` conta per denari, settebello, primiera e spincio). |

### 1.5 Fine partita
| ID | Regola |
|---|---|
| E1 | Si controlla **solo a fine smazzata**. Si vince a **31**; se entrambe le squadre sono a 31 o più, vince chi ha di più. |
| E2 | Pareggio esatto a 31 o più → **partita di spareggio** intera da 0–0 (il mazziere continua la rotazione), ripetuta finché qualcuno vince. |
| E3 | Le carte prese **non sono visibili a nessuno**, nemmeno le proprie. Il **punteggio è sempre visibile** e si aggiorna subito per spazzini e accusi. A fine smazzata c'è un riepilogo dettagliato dei punti. |

### 1.6 Backlog regole (fuori dall'MVP, da raccogliere)
- Modalità tutti contro tutti a 3.
- Altri mazzi regionali.

### 1.7 Spincione (modalità a due mazzi, dalla v1.5)
Tutte le regole dello Spincio valgono anche nello Spincione, tranne quelle cambiate qui. Si sceglie la modalità
all'inizio della partita, contro la CPU e online.

| ID | Regola |
|---|---|
| SP1 | **Due mazzi** piacentini interi: 80 carte, ogni carta c'è due volte. |
| SP2 | **Prima distribuzione** di ogni smazzata: **4 carte a testa** e 4 scoperte in tavola. Poi **3 a testa** quando le mani sono vuote: 6 distribuzioni e **76 giocate** per smazzata. La ridistribuzione con 2 o più assi in tavola (S5) resta. |
| SP3 | **Presa dello stesso seme:** se la carta giocata e **tutte** le carte prese hanno lo **stesso seme**, la squadra fa **1 punto in più**. Si somma allo spazzino e vale **anche all'ultima giocata** (dove lo spazzino vale solo con l'asso, P6). Vale per le prese per somma e con l'asso pigliatutto; la presa per uguaglianza dello stesso seme è sempre la carta gemella, che ha la sua regola (SP10). |
| SP4 | **Accusi in prima mano raddoppiati:** i punti degli accusi dichiarati nella prima distribuzione (4 carte) valgono **il doppio**. |
| SP5 | **Accusi con 4 carte** (prima distribuzione; i punti in tabella sono già raddoppiati): somma fino a **10** con carte tutte diverse o con una coppia = **4** · somma fino a 10 con **tre carte uguali** = **6** · **quattro carte uguali** = **14**, più 4 se la somma è fino a 10 (come nello Spincio 3-3-3 = 7 + 2). La doppia coppia non vale nulla in più. |
| SP6 | **Accusi nuovi**, che si sommano a quelli dello Spincio: **colore** (tutte le carte in mano dello stesso seme) = **2**; **scala** (tutte le carte in mano in fila, senza ripetizioni) = **2**. La scala **gira**: dopo il re viene l'asso, quindi valgono anche fante-cavallo-re-asso e re-asso-2. In prima mano valgono il doppio (4). |
| SP7 | Accusi con 3 carte (dalla seconda distribuzione): come lo Spincio (A1), più colore e scala (SP6), senza raddoppio. |
| SP8 | **Fine smazzata:** carte (80), denari (20) e primiera (8 sette) come F1, F2, F5, cioè 1 punto a chi ne ha di più; in caso di pari, 0. **Settebello e rebello valgono 1 punto per ogni copia** presa (fino a 2 ciascuno). Lo spincio (F6) si conta come nello Spincio: le carte doppie non allungano la scala. |
| SP9 | **Niente vittoria immediata con i denari** (F7 non vale nello Spincione), neanche prendendoli tutti. Si vince a 31 con lo spareggio come nello Spincio (E1, E2). |
| SP10 | **Gemella** (dalla v1.6): se la carta giocata prende la sua **gemella** (la stessa carta dell'altro mazzo, es. `5D` prende `5D`), la presa vale **2 punti** al posto del punto dello stesso seme (SP3). Se la presa svuota la tavola si aggiunge lo spazzino: **3 punti**, scritta "**Gemella con cappella**"; altrimenti la scritta è "**Gemella**". Vale **anche all'ultima giocata** (2 punti: lo spazzino non vale, P6; con l'asso gemello sì, quindi 3). Vale anche per l'asso che prende l'asso gemello (P8, eccezione). Con due carte uguali in tavola (P2) prendere la gemella o l'altra resta una scelta del giocatore. |

**Mariana** (solo una scritta, nessun punto): quando un **fante** prende un **5** e un **3**, di qualunque seme, compare la scritta **MARIANA**, anche se in tavola restano altre carte. Se la presa svuota la tavola, MARIANA prende il posto della scritta "Spazzino!" e lo spazzino vale normalmente (P6).

## 2. Glossario → codice

| Italiano | Codice |
|---|---|
| Carta / Seme / Rango | `Card` / `Suit {Coins, Cups, Swords, Clubs}` / `Rank {Ace=1..Seven, Jack, Knight, King}` (`Value = (int)Rank`) |
| Posto / Squadra / Mazziere | `Seat(0..3)`, `next = (s+1) % 4` / `Team {A, B}` / `Dealer` |
| Distribuzione / Smazzata / Partita / Spareggio | `Deal` / `Round` / `Match` / `TiebreakMatch` |
| Giocata / Presa / Calata / Spazzino | `Play` / `Capture` / `Drop` / `Sweep` |
| Accuso / Spincio (scala di denari) / Primiera / Rebello / Settebello | `Declaration` / `Spincio` / `Primiera` / `Rebello` / `Settebello` |
| Mariana (fante che prende 5 e 3) | `CardPlayed.IsMariana()` (`Callouts`) |

## 3. Modello (bozza)

```csharp
public enum Suit { Coins, Cups, Swords, Clubs }
public enum Rank { Ace = 1, Two, Three, Four, Five, Six, Seven, Jack, Knight, King }
public readonly record struct Card(Rank Rank, Suit Suit);
public readonly record struct Seat(int Index) { public Team Team => Index % 2 == 0 ? Team.A : Team.B; }
[Flags] public enum DeclarationKind { None = 0, LowSum = 1, LowSumWithPair = 2, ThreeOfAKind = 4 }

public abstract record Command(Seat Seat);
public sealed record PlayCard(Seat Seat, Card Card, CaptureOption? Capture) : Command(Seat);
public sealed record Declare(Seat Seat) : Command(Seat);

public static class SpincioEngine
{
    // Restituisce una Transition (non solo lo stato) per non perdere gli eventi della prima distribuzione.
    public static Transition NewMatch(ulong seed, Seat? firstDealer = null);
    public static Result<Transition> Apply(MatchState state, Command command);
    public static IReadOnlyList<Command> LegalCommands(MatchState state, Seat seat);
    public static PlayerView ViewFor(MatchState state, Seat seat);
}
public sealed record Transition(MatchState State, ImmutableArray<GameEvent> Events);
// Ogni GameEvent dichiara la propria Audience (All / Only(seat)).
```

## 4. Macchina a stati

```mermaid
stateDiagram-v2
    [*] --> Dealing: StartMatch(seed)
    Dealing --> Dealing: first deal & table has ≥2 aces → reshuffle all
    Dealing --> AwaitingPlay: deal valid
    AwaitingPlay --> AwaitingPlay: Declare(seat) [seat==ToPlay & not yet played this deal & points>0]
    AwaitingPlay --> AwaitingPlay: PlayCard [hands not all empty]
    AwaitingPlay --> Dealing: hands empty & deck not empty (deal 2, 3 — no table cards)
    AwaitingPlay --> RoundScoring: hands empty & deck empty
    RoundScoring --> MatchOver: team has all 10 coins
    RoundScoring --> MatchOver: max score ≥31 and not tied
    RoundScoring --> Dealing: tie ≥31 → TiebreakMatch (0–0)
    RoundScoring --> Dealing: nobody ≥31 → next round, dealer+1
    MatchOver --> [*]
```

## 5. Algoritmo delle prese

```
CaptureOptions(played, table):
  if played is Ace and table ≠ ∅ and no ace in table:   // P8
    return { table }
  v = played.Value
  equal = { {t} : t ∈ table, t.Value == v }
  if equal ≠ ∅: return equal                      // P2
  return { S ⊆ table : |S| ≥ 2, Σ S.Value == v }  // P1, P3 (DFS con potatura sulla somma)

LegalPlays(hand, table):
  for card in hand:
    opts = CaptureOptions(card, table)
    if opts == ∅: yield Drop(card)
    else: for o in opts: yield Capture(card, o)   // P4
```

- Le opzioni sono insiemi di **carte**, non di valori.
- **Invariante (deduzione, corretta in v1.2):** due carte di pari valore possono stare insieme in tavola **solo se entrambe vengono dalla tavola iniziale** della smazzata. Una carta calata non può mai avere lo stesso valore di una carta in tavola, altrimenti avrebbe dovuto prendere (P2, P4). La formulazione precedente ("dopo la prima giocata non ci sono carte di pari valore") era sbagliata: se la tavola iniziale è `[5C,5S,…]` e il primo giocatore cala un `K` che non prende, i due 5 restano in tavola.
- **Dopo una presa:**
  1. le carte vanno nel mazzetto della squadra;
  2. `LastCapturingTeam` = squadra del giocatore;
  3. se la tavola è vuota e non è la 36ª giocata (76ª nello Spincione), oppure la carta giocata è un asso → +1 spazzino.

## 6. Casi limite

| # | Caso | Comportamento |
|---|---|---|
| C1 | Tavola vuota | Qualunque carta si cala |
| C2 | In tavola c'è una carta uguale e anche una somma possibile | Solo uguaglianza |
| C3 | Due carte uguali in tavola (solo tavola iniziale) | Se ne prende una, a scelta |
| C4 | Una carta in mano prende, un'altra no | Si può calare quella che non prende; quella che prende non si può calare |
| C5 | Spazzino all'ultima giocata | Si prendono le carte, lo spazzino non vale; con l'asso sì (P6, v1.7) |
| C6 | Calata all'ultima giocata | Tavola e carta calata vanno a chi ha preso per ultimo |
| C7 | Nessuno prende in tutta la smazzata | Carte rimaste a nessuno |
| C8 | Tavola iniziale con 2, 3 o 4 assi | Si rimescola tutto, stesso mazziere |
| C9 | Accuso non dichiarato prima della propria prima giocata | Perso |
| C10 | Mano da 0 punti | `Declare` illegale |
| C11 | Una squadra arriva a 31 a metà smazzata | Si continua |
| C12 | Tutti i denari presi, ma l'avversario ha più punti | Vince chi ha i denari |
| C13 | Pareggi 20–20 carte, 5–5 denari, 2–2 sette | 0 punti |
| C14 | Spincio con buchi o senza asso | 0 punti |
| C15 | Pareggio a 31 o più ripetuto | Altro spareggio |
| C16 | Asso giocato con carte in tavola | Prende tutta la tavola ed è spazzino (P8); se c'è un asso in tavola prende solo quello |

## 7. Test di accettazione

Ogni test nel codice porta lo stesso ID (es. `AT_05_equal_value_forbids_sum`).

### Setup e determinismo
- **AT-01** Nuova smazzata → ogni posto ha 3 carte, la tavola 4, il mazzo 24.
- **AT-02** Seed con 2 o più assi in tavola → si rimescola finché la tavola ha al massimo 1 asso; con lo stesso seed lo stato finale è identico.
- **AT-03** Mazziere al posto 1 → gioca per primo il posto 2; alla smazzata successiva il mazziere è il posto 2.
- **AT-04** Stesso seed + stessi comandi → stati ed eventi identici.

### Prese
- **AT-05** Tavola `[5C,3S,2B]`, gioco `5D` → unica opzione `{5C}`; `{3S,2B}` non ammessa.
- **AT-06** Tavola `[4C,3S,6B]`, gioco `7D` → `{4C,3S}`.
- **AT-07** Tavola `[AC,6S,2B,5D]`, gioco `7C` → opzioni `{AC,6S}` e `{2B,5D}`, sceglie il giocatore.
- **AT-08** Tavola `[7C,3S]`, gioco `KB` → prendo `{7C,3S}`.
- **AT-09** Tavola `[JC,2S]`, gioco `KD` → prendo `{JC,2S}`.
- **AT-10** Tavola `[5C,5S]`, gioco `5D` → opzioni `{5C}` oppure `{5S}`, mai entrambe.
- **AT-11** Mano `[7D,KC]`, tavola `[3S,4B]` → calare `KC` è legale; calare `7D` è illegale; `7D` con `{3S,4B}` è legale.
- **AT-12** Tavola `[6C]`, gioco `3S` → `3S` resta in tavola.

### Asso pigliatutto (P8)
- **AT-33** Tavola `[3S,KB,7C]`, gioco `AD` → unica opzione: tutta la tavola.
- **AT-34** Tavola `[AC,5S,JB]`, gioco `AD` → unica opzione `{AC}` (presa normale).
- **AT-35** Non è l'ultima giocata, tavola `[3S,KB,7C]`, gioco `AD` → tavola vuota, +1 spazzino.
- **AT-36** Tavola vuota, gioco `AD` → l'asso si cala, nessun punto.
- **AT-37** Ultima giocata, tavola `[3S,4B]`, gioco `AD` → prende tutto ed è spazzino, +1 (v1.7; prima lo spazzino non valeva).
- **AT-38** Tavola `[3S,5B]`: calare `AD` o prendere solo `{3S}` è illegale. Tavola `[AC,5S]`: calare `AD` o prendere `{AC,5S}` è illegale.
- **AT-39** Non è l'ultima giocata, tavola `[AC]`, gioco `AD` → prende `AC`, tavola vuota, +1 spazzino.
- **AT-52** Ultima giocata (v1.7): tavola `[AC]`, gioco `AD` → prende `AC`, +1 spazzino · tavola `[AC,5S]`, gioco `AD` → prende `AC`, niente spazzino (resta `5S`) · Spincione, tavola `[AD]`, gioco `AD` → gemella con cappella, 3 punti · Spincione, tavola `[3S,5S]`, gioco `AS` → spazzino e stesso seme, 2 punti. Con le altre carte resta AT-14.

### Scritte
- **AT-40** Mariana: `JD` prende `{5S,3C}` → MARIANA; non lo sono `JC` che prende `{6S,2C}`, `AD` che prende `{5S,3C}` (P8) né `KD` che prende `{5S,3C,2B}`. Tavola `[5S,3C,KB]`, gioco `JD` prende `{5S,3C}` → MARIANA, nessun punto. Tavola `[5S,3C]` (non ultima giocata) → MARIANA e spazzino, +1.

### Spazzino e fine smazzata
- **AT-13** Tavola `[3S,4B]`, non è l'ultima giocata, gioco `7D` → +1 spazzino alla squadra.
- **AT-14** Stessa situazione all'ultima giocata → prendo le carte, lo spazzino non vale.
- **AT-15** Ultima presa della squadra B, restano `[2C,6S]` → vanno alla squadra B, senza spazzino.

### Accusi
- **AT-16** Punti per mano: `A-3-5` → 2 · `2-2-5` → 3 · `2-2-6` → 0 · `3-3-3` → 9 · `5-5-5` → 7 · `K-K-K` → 7 · `A-A-7` → 3 · `K-2-A` → 0.
- **AT-17** Mano da 0 punti → `Declare` illegale.
- **AT-18** Il posto 2 ha un accuso valido ma tocca al posto 1 → `Declare(2)` illegale. Arriva il turno del posto 2 → `Declare(2)` legale finché il posto 2 non gioca. Il posto 2 gioca senza dichiarare → accuso perso per quella distribuzione.
- **AT-19** Accuso dichiarato → gli altri vedono solo tipo e punti.
- **AT-20** Accuso valido alla 2ª distribuzione → dichiarabile anche lì.

### Punti di fine smazzata
- **AT-21** Carte 21–19 → +1 alla squadra con 21; 20–20 → 0.
- **AT-22** Denari 6–4 → +1; 5–5 → 0.
- **AT-23** `KD` e `7D` nel mazzetto della squadra A → +1 e +1 alla squadra A.
- **AT-24** Sette 3–1 → +1 primiera; 2–2 → 0.
- **AT-25** Spincio: `A,2,3` → 3 · `A…5` → 5 · `A,2,4` → 0 · `2,3,4` → 0 · `A…N` → 9.
- **AT-26** La squadra A ha tutti i 10 denari → vince la partita anche se B ha più punti.

### Fine partita
- **AT-27** A a 29 dichiara un accuso da 3 punti a metà smazzata → la partita non finisce prima della fine della smazzata.
- **AT-28** 33–32 a fine smazzata → vince A.
- **AT-29** 31–20 → vince A.
- **AT-30** 32–32 → spareggio da 0–0; se finisce ancora pari, un altro spareggio.

### Spincione (§1.7)
- **AT-41** Nuova smazzata dello Spincione → ogni posto ha 4 carte, la tavola 4, il mazzo 60. Le 80 carte contengono ogni carta esattamente due volte.
- **AT-42** Dopo la prima distribuzione ogni distribuzione dà 3 carte a testa; la smazzata finisce dopo 76 giocate (6 distribuzioni).
- **AT-43** Presa dello stesso seme: tavola `[3D,4D]`, gioco `7D` (non ultima giocata) → +1 spazzino e +1 stesso seme; tavola `[3D,4C]`, gioco `7D` → nessun punto di seme; tavola `[3S,5S,KB]`, gioco `AS` → nessun punto di seme (`KB` è di bastoni); tavola `[3S,5S]`, gioco `AS` → +1 spazzino e +1 stesso seme; tavola `[2D,3D,KC]`, gioco `5D` → prende `2D,3D`, +1 stesso seme. (La gemella è in AT-51.)
- **AT-44** All'ultima giocata, tavola `[3D,4D]`, gioco `7D` → lo spazzino non vale, il punto dello stesso seme sì. Nello Spincio la presa dello stesso seme non vale nulla.
- **AT-45** Accusi in prima mano (4 carte, già raddoppiati): `AC,2S,5B,2D` → 4 · `2C,2S,2B,4D` → 6 · `3C,3S,3B,3D` → 14 · `2C,2S,2B,2D` → 18 · `AC,AS,2B,2D` → 4 · `5C,5S,NB,ND` → 0 · `7C,7S,7B,KD` → 0 · `5D,7D,ND,KD` → 4 (colore) · `JC,NS,KB,AD` → 4 (scala) · `KC,AS,2B,3D` → 4 (scala) · `AD,2C,3S,4B` → 8 (somma 10 + scala).
- **AT-46** Accusi con 3 carte nello Spincione: `AD,2D,3D` → 6 (somma + colore + scala) · `NC,KS,AB` → 2 (scala) · `KC,AC,2C` → 4 (scala + colore) · `2C,2S,5B` → 3 · `3C,3S,3B` → 9 · `5D,5D,ND` → 2 (colore; la coppia vale solo con la somma).
- **AT-47** Fine smazzata dello Spincione: la squadra A ha i due `7D` → 2 punti di settebello; un `KD` a testa → 1 punto di rebello a testa; carte 40–40, denari 10–10, sette 4–4 → 0.
- **AT-48** Nello Spincione una squadra con tutti i 20 denari non vince subito: la partita continua (lo spincio A…K vale 10).
- **AT-49** Due carte gemelle in tavola: tavola `[5D,5D]`, gioco `5S` → una sola opzione `{5D}`; dopo la presa in tavola resta un `5D`.
- **AT-50** La modalità fa parte della partita: stesso seed e stessi comandi danno la stessa partita, e lo Spincio con lo stesso seed resta identico a prima della v1.5.
- **AT-51** Gemella (SP10): tavola `[5D,KC]`, gioco `5D` → 2 punti, nessun punto dello stesso seme · tavola `[5D]`, gioco `5D` (non ultima giocata) → 3 punti (gemella con cappella) · tavola `[5D,5C,KC]`, gioco `5D`: prendendo `5D` → 2 punti, prendendo `5C` → 0 · tavola `[AD,3C]`, gioco `AD` → prende solo `AD`, 2 punti · all'ultima giocata, tavola `[5D]`, gioco `5D` → 2 punti, niente spazzino; il riepilogo della smazzata conta 2 punti di gemelle.

### Informazioni nascoste
- **AT-31** La `PlayerView` contiene: la propria mano, la tavola, il punteggio, gli accusi dichiarati (tipo e punti), il numero di carte in mano agli altri. **Non** contiene nessun mazzetto di carte prese.
- **AT-32** Nessuno ha preso in tutta la smazzata → le carte rimaste in tavola non vanno a nessuno; contano solo le altre voci.
