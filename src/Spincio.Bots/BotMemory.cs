using System.Collections.Immutable;
using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>
/// What a seat remembers of the current round, exactly as a player with perfect memory would:
/// every card it has legitimately seen (initial table, its own hands, played and captured cards),
/// which team captured which cards, and what each seat played in the current deal.
/// Feed it only the events addressed to this seat (<see cref="Transition.EventsFor"/>).
/// </summary>
/// <remarks>
/// Cards are counted, not just listed: in the Spincione every card exists twice (SP1), so seeing a 7 of coins
/// leaves one more unseen. A card captured from the table was already seen there and is not counted again.
/// </remarks>
public sealed class BotMemory
{
    public const int CardsPerValue = 4;

    private readonly List<Card> _seen = [];
    private readonly List<Card> _table = [];
    private readonly List<Card>[] _captured = [[], []];
    private readonly List<Card>[] _playedThisDeal = [[], [], [], []];

    public BotMemory(Seat seat, GameVariant variant = GameVariant.Spincio)
    {
        Seat = seat;
        Variant = variant;
    }

    public Seat Seat { get; }

    /// <summary>The game mode, learned from <see cref="RoundStarted"/>: it tells how many copies of each card exist.</summary>
    public GameVariant Variant { get; private set; }

    /// <summary>Every card seen this round, once per physical card (twice if both copies were seen).</summary>
    public IReadOnlyList<Card> Seen => _seen;

    /// <summary>Cards this seat has never seen this round (in other hands or in the deck).</summary>
    public int UnseenCount => Variant.CardCount() - _seen.Count;

    /// <summary>Captured cards per team, as witnessed on the table (the piles themselves stay hidden, E3).</summary>
    public ImmutableArray<Card> CapturedBy(Team team) => [.. _captured[(int)team]];

    public Team? LastCapturingTeam { get; private set; }

    /// <summary>Cards the seat has played since the current deal started.</summary>
    public ImmutableArray<Card> PlayedThisDeal(Seat seat) => [.. _playedThisDeal[seat.Index]];

    public int UnseenOfValue(int value) => (CardsPerValue * Variant.DeckCopies()) - _seen.Count(c => c.Value == value);

    /// <summary>The unseen cards, each copy separately, in canonical order.</summary>
    public IEnumerable<Card> Unseen()
    {
        var unseen = new List<Card>();
        int copies = Variant.DeckCopies();
        foreach (var card in Card.FullDeck)
        {
            int left = copies - _seen.Count(c => c == card);
            for (int i = 0; i < left; i++)
            {
                unseen.Add(card);
            }
        }

        return unseen;
    }

    public void Observe(IEnumerable<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var e in events)
        {
            Observe(e);
        }
    }

    public void Observe(GameEvent gameEvent)
    {
        ArgumentNullException.ThrowIfNull(gameEvent);
        if (!gameEvent.Audience.Includes(Seat))
        {
            throw new InvalidOperationException($"{Seat} received an event it must not see: {gameEvent.GetType().Name}");
        }

        switch (gameEvent)
        {
            case RoundStarted started:
                Variant = started.Variant;
                _seen.Clear();
                _table.Clear();
                _captured[0].Clear();
                _captured[1].Clear();
                LastCapturingTeam = null;
                ClearDeal();
                break;
            case DealStarted deal:
                if (deal.DealNumber == 1)
                {
                    _seen.AddRange(deal.Table);
                    _table.Clear();
                    _table.AddRange(deal.Table);
                }

                ClearDeal();
                break;
            case HandDealt dealt:
                _seen.AddRange(dealt.Cards);
                break;
            case CardPlayed played:
                Observe(played);
                break;
        }
    }

    private void Observe(CardPlayed played)
    {
        if (played.Seat != Seat)
        {
            _seen.Add(played.Card); // our own card was seen in our hand
        }

        _playedThisDeal[played.Seat.Index].Add(played.Card);
        if (played.Captured.IsEmpty)
        {
            _table.Add(played.Card);
            return;
        }

        foreach (var card in played.Captured)
        {
            if (!_table.Remove(card))
            {
                _seen.Add(card); // a table card we had not seen (e.g. a memory started mid-round)
            }
        }

        var team = played.Seat.Team;
        _captured[(int)team].AddRange(played.Captured);
        _captured[(int)team].Add(played.Card);
        LastCapturingTeam = team;
    }

    private void ClearDeal()
    {
        foreach (var list in _playedThisDeal)
        {
            list.Clear();
        }
    }
}
