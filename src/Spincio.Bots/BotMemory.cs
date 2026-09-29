using System.Collections.Immutable;
using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>
/// What a seat remembers of the current round, exactly as a player with perfect memory would:
/// every card it has legitimately seen (initial table, its own hands, played and captured cards),
/// which team captured which cards, and what each seat played in the current deal.
/// Feed it only the events addressed to this seat (<see cref="Transition.EventsFor"/>).
/// </summary>
public sealed class BotMemory
{
    public const int CardsPerValue = 4;

    private readonly HashSet<Card> _seen = [];
    private readonly List<Card>[] _captured = [[], []];
    private readonly List<Card>[] _playedThisDeal = [[], [], [], []];

    public BotMemory(Seat seat)
    {
        Seat = seat;
    }

    public Seat Seat { get; }

    public IReadOnlySet<Card> Seen => _seen;

    /// <summary>Cards this seat has never seen this round (in other hands or in the deck).</summary>
    public int UnseenCount => Card.FullDeck.Count - _seen.Count;

    /// <summary>Captured cards per team, as witnessed on the table (the piles themselves stay hidden, E3).</summary>
    public ImmutableArray<Card> CapturedBy(Team team) => [.. _captured[(int)team]];

    public Team? LastCapturingTeam { get; private set; }

    /// <summary>Cards the seat has played since the current deal started.</summary>
    public ImmutableArray<Card> PlayedThisDeal(Seat seat) => [.. _playedThisDeal[seat.Index]];

    public int UnseenOfValue(int value) => CardsPerValue - _seen.Count(c => c.Value == value);

    public IEnumerable<Card> Unseen() => Card.FullDeck.Where(c => !_seen.Contains(c));

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
            case RoundStarted:
                _seen.Clear();
                _captured[0].Clear();
                _captured[1].Clear();
                LastCapturingTeam = null;
                ClearDeal();
                break;
            case DealStarted deal:
                if (deal.DealNumber == 1)
                {
                    _seen.UnionWith(deal.Table);
                }

                ClearDeal();
                break;
            case HandDealt dealt:
                _seen.UnionWith(dealt.Cards);
                break;
            case CardPlayed played:
                _seen.Add(played.Card);
                _seen.UnionWith(played.Captured);
                _playedThisDeal[played.Seat.Index].Add(played.Card);
                if (!played.Captured.IsEmpty)
                {
                    var team = played.Seat.Team;
                    _captured[(int)team].AddRange(played.Captured);
                    _captured[(int)team].Add(played.Card);
                    LastCapturingTeam = team;
                }

                break;
        }
    }

    private void ClearDeal()
    {
        foreach (var list in _playedThisDeal)
        {
            list.Clear();
        }
    }
}
