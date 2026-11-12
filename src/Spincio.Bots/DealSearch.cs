using System.Numerics;
using System.Runtime.CompilerServices;
using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>
/// Searches one sampled world for <see cref="LookaheadBot"/>: every play to a given depth, our team maximising and the
/// opponents minimising the value. The rules of a play are rewritten here for speed (a compact copy-on-play state, no
/// allocations); they must agree with <see cref="SpincioEngine"/> on every play, which tests check against the engine.
/// Declarations are left out: they do not depend on the card played.
/// </summary>
internal sealed class DealSearch
{
    /// <summary>
    /// Room for the table. A dropped card never matches a table card (P2, P4), so only the initial table repeats values:
    /// at most 10 values plus 3 repeats.
    /// </summary>
    public const int MaxTable = 16;
    private const int HandSlots = 4;
    private const int MaxDepth = 24;
    private const int MaxMoves = 96;

    private static readonly Card[] Cards = [.. Card.FullDeck];

    /// <summary>L1's card values: only to try promising plays first, so pruning cuts more.</summary>
    private static readonly double[] OrderWeight = [.. Card.FullDeck.Select(new GreedyBot().CardValue)];

    private readonly GameVariant _variant;
    private readonly double _allCoinsValue;
    private readonly bool _bonuses;
    private readonly int _playsPerRound;
    private readonly byte[] _deck;
    private readonly Move[][] _moves;
    private readonly State[] _states;
    private int _deckLength;
    private int _dealer;
    private double _rootOutlook;
    private Team _us;

    /// <param name="allCoinsValue">Value of winning all 10 coins (F7) relative to one point.</param>
    public DealSearch(GameVariant variant, double allCoinsValue)
    {
        _variant = variant;
        _allCoinsValue = allCoinsValue;
        _bonuses = variant.HasSameSuitBonus();
        _playsPerRound = variant.PlaysPerRound();
        _deck = new byte[variant.CardCount()];
        _moves = new Move[MaxDepth + 1][];
        for (int i = 0; i < _moves.Length; i++)
        {
            _moves[i] = new Move[MaxMoves];
        }

        _states = new State[MaxDepth + 2];
    }

    /// <summary>Positions searched since the search was created: the measure of its cost.</summary>
    public long Nodes { get; private set; }

    public static int Id(Card card) => ((int)card.Suit * 10) + card.Value - 1;

    /// <summary>False if the world does not fit the compact state (never in a real game; the caller then decides otherwise).</summary>
    public bool Load(MatchState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Table.Length > MaxTable || world.Hands.Any(h => h.Length > HandSlots) || world.Phase != MatchPhase.AwaitingPlay)
        {
            return false;
        }

        ref var root = ref _states[0];
        root = default;
        for (int seat = 0; seat < Seat.Count; seat++)
        {
            var hand = world.Hands[seat];
            for (int i = 0; i < hand.Length; i++)
            {
                root.Hands[(seat * HandSlots) + i] = (byte)Id(hand[i]);
            }

            root.HandCounts[seat] = hand.Length;
        }

        for (int i = 0; i < world.Table.Length; i++)
        {
            root.Table[i] = (byte)Id(world.Table[i]);
        }

        root.TableCount = world.Table.Length;
        _deckLength = world.Deck.Length;
        for (int i = 0; i < world.Deck.Length; i++)
        {
            _deck[i] = (byte)Id(world.Deck[i]);
        }

        root.DealNumber = world.DealNumber;
        root.Plays = world.PlaysInRound;
        root.ToPlay = world.ToPlay.Index;
        root.LastCapturing = world.LastCapturingTeam is { } last ? (int)last : -1;
        root.A = CardTally.Of(world.PileOf(Team.A));
        root.B = CardTally.Of(world.PileOf(Team.B));
        _dealer = world.Dealer.Index;
        _rootOutlook = RoundOutlook.Expected(root.A, root.B, _variant, _allCoinsValue);
        return true;
    }

    /// <summary>
    /// Value for <paramref name="us"/> of playing <paramref name="first"/> in the loaded world, then <paramref name="plies"/>
    /// more plays with every seat playing its best: points scored on the way, plus the round points the captures are
    /// expected to bring (<see cref="RoundOutlook"/>), exact if the round ends within the search.
    /// </summary>
    public double Evaluate(PlayCard first, Team us, int plies)
    {
        ArgumentNullException.ThrowIfNull(first);
        _us = us;
        ref var root = ref _states[0];
        int mask = 0;
        if (first.Capture is { } capture)
        {
            foreach (var taken in capture.Cards)
            {
                int id = Id(taken);
                int position = -1;
                for (int i = 0; i < root.TableCount; i++)
                {
                    if (root.Table[i] == id && (mask & (1 << i)) == 0)
                    {
                        position = i;
                        break;
                    }
                }

                if (position < 0)
                {
                    throw new ArgumentException($"{taken} is not on the table.", nameof(first));
                }

                mask |= 1 << position;
            }
        }

        bool roundOver = Play(0, Id(first.Card), mask);
        return roundOver ? RoundValue(ref _states[1]) : Search(1, Math.Min(plies, MaxDepth), double.NegativeInfinity, double.PositiveInfinity);
    }

    private double Search(int depth, int pliesLeft, double alpha, double beta)
    {
        Nodes++;
        ref var state = ref _states[depth];
        if (pliesLeft <= 0)
        {
            return Leaf(ref state);
        }

        var moves = _moves[depth];
        int count = Generate(ref state, moves);
        if (count == 0)
        {
            return Leaf(ref state);
        }

        bool ours = (state.ToPlay % 2 == 0 ? Team.A : Team.B) == _us;
        double best = ours ? double.NegativeInfinity : double.PositiveInfinity;
        for (int m = 0; m < count; m++)
        {
            bool roundOver = Play(depth, moves[m].Card, moves[m].Mask);
            double value = roundOver ? RoundValue(ref _states[depth + 1]) : Search(depth + 1, pliesLeft - 1, alpha, beta);
            if (ours)
            {
                best = Math.Max(best, value);
                alpha = Math.Max(alpha, value);
            }
            else
            {
                best = Math.Min(best, value);
                beta = Math.Min(beta, value);
            }

            if (beta <= alpha)
            {
                break;
            }
        }

        return best;
    }

    /// <summary>Every play of the seat to move (P1–P5, P8), most promising first for that seat.</summary>
    private int Generate(ref State state, Move[] moves)
    {
        int seat = state.ToPlay;
        int tableCount = state.TableCount;
        Span<int> order = stackalloc int[MaxTable];
        for (int i = 0; i < tableCount; i++)
        {
            order[i] = i;
        }

        // Ascending value, then card: the order the engine enumerates sums in, so identical cards are skipped alike.
        for (int i = 1; i < tableCount; i++)
        {
            int key = order[i];
            int j = i - 1;
            while (j >= 0 && Compare(state.Table[order[j]], state.Table[key]) > 0)
            {
                order[j + 1] = order[j];
                j--;
            }

            order[j + 1] = key;
        }

        bool aceOnTable = false;
        for (int i = 0; i < tableCount; i++)
        {
            aceOnTable |= Cards[state.Table[i]].Rank == Rank.Ace;
        }

        int count = 0;
        int slot = seat * HandSlots;
        for (int h = 0; h < state.HandCounts[seat]; h++)
        {
            int card = state.Hands[slot + h];
            bool repeated = false;
            for (int k = 0; k < h; k++)
            {
                repeated |= state.Hands[slot + k] == card;
            }

            if (repeated)
            {
                continue; // two identical cards in hand are one choice (Spincione)
            }

            int value = Cards[card].Value;
            int before = count;
            if (value == 1 && tableCount > 0 && !aceOnTable)
            {
                count = Add(ref state, moves, count, card, (1 << tableCount) - 1); // P8
                continue;
            }

            for (int i = 0; i < tableCount; i++)
            {
                int id = state.Table[order[i]];
                if (Cards[id].Value == value && (i == 0 || state.Table[order[i - 1]] != id))
                {
                    count = Add(ref state, moves, count, card, 1 << order[i]); // P2: the equal card, one at a time
                }
            }

            if (count == before)
            {
                count = Sums(ref state, order, 0, 0, value, 0, card, moves, count); // P1, P3
            }

            if (count == before)
            {
                count = Add(ref state, moves, count, card, 0); // nothing to take: dropped
            }
        }

        for (int i = 1; i < count; i++)
        {
            var key = moves[i];
            int j = i - 1;
            while (j >= 0 && moves[j].Gain < key.Gain)
            {
                moves[j + 1] = moves[j];
                j--;
            }

            moves[j + 1] = key;
        }

        return count;
    }

    private static int Sums(ref State state, Span<int> order, int start, int sum, int target, int mask, int card, Move[] moves, int count)
    {
        for (int i = start; i < state.TableCount; i++)
        {
            int id = state.Table[order[i]];
            if (i > start && state.Table[order[i - 1]] == id)
            {
                continue; // an identical card in the same position would repeat the same set (SP1)
            }

            int next = sum + Cards[id].Value;
            if (next > target)
            {
                return count; // ascending values: nothing further fits
            }

            int chosen = mask | (1 << order[i]);
            if (next < target)
            {
                count = Sums(ref state, order, i + 1, next, target, chosen, card, moves, count);
            }
            else if (BitOperations.PopCount((uint)chosen) >= 2)
            {
                count = Add(ref state, moves, count, card, chosen);
            }
        }

        return count;
    }

    private static int Add(ref State state, Move[] moves, int count, int card, int mask)
    {
        if (count >= MaxMoves)
        {
            return count;
        }

        double gain = mask == 0 ? -0.3 * OrderWeight[card] : OrderWeight[card];
        for (int i = 0; i < state.TableCount; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                gain += OrderWeight[state.Table[i]];
            }
        }

        if (mask != 0 && BitOperations.PopCount((uint)mask) == state.TableCount)
        {
            gain += 10; // a sweep
        }

        moves[count] = new Move((byte)card, mask, gain);
        return count + 1;
    }

    /// <summary>Plays from <c>_states[depth]</c> into <c>_states[depth + 1]</c>, as the engine does; true if that ended the round.</summary>
    private bool Play(int depth, int card, int mask)
    {
        ref var child = ref _states[depth + 1];
        child = _states[depth];
        int seat = child.ToPlay;
        int team = seat % 2;

        int slot = seat * HandSlots;
        int last = child.HandCounts[seat] - 1;
        for (int i = 0; i <= last; i++)
        {
            if (child.Hands[slot + i] == card)
            {
                child.Hands[slot + i] = child.Hands[slot + last];
                break;
            }
        }

        child.HandCounts[seat] = last;
        child.Plays++;
        bool isLastPlay = child.Plays == _playsPerRound;

        if (mask == 0)
        {
            child.Table[child.TableCount++] = (byte)card;
        }
        else
        {
            ref var pile = ref team == 0 ? ref child.A : ref child.B;
            int kept = 0, taken = 0, takenCard = -1;
            bool sameSuit = true;
            var suit = Cards[card].Suit;
            for (int i = 0; i < child.TableCount; i++)
            {
                int id = child.Table[i];
                if ((mask & (1 << i)) != 0)
                {
                    pile.Add(Cards[id]);
                    sameSuit &= Cards[id].Suit == suit;
                    takenCard = id;
                    taken++;
                }
                else
                {
                    child.Table[kept++] = (byte)id;
                }
            }

            pile.Add(Cards[card]);
            child.TableCount = kept;
            child.LastCapturing = team;
            int points = kept == 0 && (!isLastPlay || Cards[card].Rank == Rank.Ace) ? 1 : 0; // P6, P8
            if (_bonuses)
            {
                bool twin = taken == 1 && takenCard == card;
                points += twin ? VariantRules.TwinPoints : sameSuit ? 1 : 0; // SP10, SP3
            }

            if (team == 0)
            {
                child.LiveA += points;
            }
            else
            {
                child.LiveB += points;
            }
        }

        child.ToPlay = (seat + 1) % Seat.Count;
        if (child.HandCounts[0] + child.HandCounts[1] + child.HandCounts[2] + child.HandCounts[3] > 0)
        {
            return false;
        }

        if (child.DeckPosition >= _deckLength)
        {
            return true;
        }

        int handSize = _variant.HandSize(child.DealNumber + 1); // S4, SP2: dealt from the seat after the dealer
        for (int k = 1; k <= Seat.Count; k++)
        {
            int dealt = (_dealer + k) % Seat.Count;
            for (int i = 0; i < handSize; i++)
            {
                child.Hands[(dealt * HandSlots) + i] = _deck[child.DeckPosition++];
            }

            child.HandCounts[dealt] = handSize;
        }

        child.DealNumber++;
        return false;
    }

    private double Leaf(ref State state)
    {
        double value = state.LiveA - state.LiveB + RoundOutlook.Expected(state.A, state.B, _variant, _allCoinsValue) - _rootOutlook;
        return _us == Team.A ? value : -value;
    }

    /// <summary>The round is over: the table goes to the last team that took (P7), then the round is scored (F1–F7).</summary>
    private double RoundValue(ref State state)
    {
        var a = state.A;
        var b = state.B;
        for (int i = 0; i < state.TableCount; i++)
        {
            if (state.LastCapturing == 0)
            {
                a.Add(Cards[state.Table[i]]);
            }
            else if (state.LastCapturing == 1)
            {
                b.Add(Cards[state.Table[i]]);
            }
        }

        double value = state.LiveA - state.LiveB + RoundOutlook.Exact(a, b, _variant, _allCoinsValue);
        return _us == Team.A ? value : -value;
    }

    private static int Compare(int x, int y)
    {
        var a = Cards[x];
        var b = Cards[y];
        return a.Value != b.Value ? a.Value.CompareTo(b.Value) : a.CompareTo(b);
    }

    private readonly record struct Move(byte Card, int Mask, double Gain);

    private struct State
    {
        public CardBuffer Hands;
        public CardBuffer Table;
        public CountBuffer HandCounts;
        public int TableCount;
        public int DeckPosition;
        public int DealNumber;
        public int Plays;
        public int ToPlay;
        public int LastCapturing;

        /// <summary>Points scored during the search (sweeps P6, SP3, SP10), by team.</summary>
        public int LiveA;
        public int LiveB;
        public CardTally A;
        public CardTally B;
    }

    [InlineArray(16)]
    private struct CardBuffer
    {
        private byte _card;
    }

    [InlineArray(4)]
    private struct CountBuffer
    {
        private int _count;
    }
}
