using Spincio.Engine;

namespace Spincio.Bots;

/// <summary>
/// What a seat remembers of the current round: every card it has legitimately seen
/// (initial table, its own hands, played and captured cards). Feed it only the events
/// addressed to this seat (<see cref="Transition.EventsFor"/>).
/// </summary>
public sealed class BotMemory
{
    public const int CardsPerValue = 4;

    private readonly HashSet<Card> _seen = [];

    public BotMemory(Seat seat)
    {
        Seat = seat;
    }

    public Seat Seat { get; }

    public IReadOnlySet<Card> Seen => _seen;

    /// <summary>Cards this seat has never seen this round (in other hands or in the deck).</summary>
    public int UnseenCount => Card.FullDeck.Count - _seen.Count;

    public int UnseenOfValue(int value) => CardsPerValue - _seen.Count(c => c.Value == value);

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
                break;
            case DealStarted { DealNumber: 1 } deal:
                _seen.UnionWith(deal.Table);
                break;
            case HandDealt dealt:
                _seen.UnionWith(dealt.Cards);
                break;
            case CardPlayed played:
                _seen.Add(played.Card);
                _seen.UnionWith(played.Captured);
                break;
        }
    }
}
