using System.Collections.Immutable;
using Spincio.Engine;

namespace Spincio.Engine.Tests.Support;

/// <summary>
/// Builds an arbitrary mid-round <see cref="MatchState"/>. Cards not placed explicitly go to
/// <see cref="Leftovers"/> (the deck by default) so the 40 cards are always conserved.
/// </summary>
internal sealed class Scenario
{
    private readonly ImmutableArray<Card>[] _hands = [[], [], [], []];
    private readonly ImmutableArray<Card>[] _piles = [[], []];
    private ImmutableArray<Card> _table = [];
    private ImmutableArray<Card>? _deck;

    public enum LeftoverTarget
    {
        Deck,
        PileA,
        PileB,
    }

    public Seat ToPlay { get; set; } = new(0);

    public Seat Dealer { get; set; } = new(3);

    public int DealNumber { get; set; } = 1;

    public int PlaysInRound { get; set; }

    public TeamScores Score { get; set; }

    public Team? LastCapturingTeam { get; set; }

    public int MatchNumber { get; set; }

    public LeftoverTarget Leftovers { get; set; } = LeftoverTarget.Deck;

    public ImmutableArray<bool> PlayedThisDeal { get; set; } = [false, false, false, false];

    public Scenario Hand(int seat, string cards)
    {
        _hands[seat] = Cards.Many(cards);
        return this;
    }

    public Scenario Table(string cards)
    {
        _table = Cards.Many(cards);
        return this;
    }

    public Scenario Pile(Team team, IEnumerable<Card> cards)
    {
        _piles[(int)team] = [.. cards];
        return this;
    }

    /// <summary>Explicit deck; otherwise the deck holds the leftovers (or is empty if leftovers go to a pile).</summary>
    public Scenario Deck(string cards)
    {
        _deck = Cards.Many(cards);
        return this;
    }

    /// <summary>Last play of the round: deck empty, only <see cref="ToPlay"/> holds one card.</summary>
    public Scenario LastPlay(LeftoverTarget leftovers)
    {
        PlaysInRound = MatchState.PlaysPerRound - 1;
        DealNumber = 3;
        _deck = [];
        Leftovers = leftovers;
        return this;
    }

    public MatchState Build()
    {
        var placed = _hands.SelectMany(h => h).Concat(_table).Concat(_piles.SelectMany(p => p)).Concat(_deck ?? []).ToList();
        if (placed.Count != placed.Distinct().Count())
        {
            throw new InvalidOperationException("A card was placed twice.");
        }

        var rest = Card.FullDeck.Except(placed).ToImmutableArray();
        var piles = _piles.ToArray();
        var deck = _deck ?? [];
        switch (Leftovers)
        {
            case LeftoverTarget.Deck:
                deck = deck.AddRange(rest);
                break;
            case LeftoverTarget.PileA:
                piles[0] = piles[0].AddRange(rest);
                break;
            case LeftoverTarget.PileB:
                piles[1] = piles[1].AddRange(rest);
                break;
        }

        return new MatchState
        {
            Rng = Pcg32.FromSeed(12345),
            Phase = MatchPhase.AwaitingPlay,
            MatchNumber = MatchNumber,
            RoundNumber = 1,
            Dealer = Dealer,
            ToPlay = ToPlay,
            DealNumber = DealNumber,
            PlaysInRound = PlaysInRound,
            Hands = [.. _hands],
            Table = _table,
            Deck = deck,
            Piles = [.. piles],
            Unclaimed = [],
            LastCapturingTeam = LastCapturingTeam,
            Score = Score,
            Ledger = RoundLedger.Empty,
            PlayedThisDeal = PlayedThisDeal,
            Declarations = [],
            Winner = null,
            Sequence = 0,
        };
    }
}
