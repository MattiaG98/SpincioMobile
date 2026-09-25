using System.Collections.Immutable;

namespace Spincio.Engine;

/// <summary>Pure rules engine: <c>Apply(state, command) → (state, events)</c>. No I/O, no clock, no ambient randomness.</summary>
public static class SpincioEngine
{
    /// <summary>
    /// Starts a match: random first dealer unless given (S3), then the first round is dealt.
    /// Returns a <see cref="Transition"/> so the initial deal events are not lost.
    /// </summary>
    public static Transition NewMatch(ulong seed, Seat? firstDealer = null)
    {
        var rng = Pcg32.FromSeed(seed);
        var dealer = firstDealer ?? new Seat(rng.NextInt(Seat.Count));
        var events = ImmutableArray.CreateBuilder<GameEvent>();
        var state = StartRound(rng, dealer, matchNumber: 0, roundNumber: 1, TeamScores.Zero, sequence: 0, events);
        return new Transition(state, events.ToImmutable());
    }

    public static Result<Transition> Apply(MatchState state, Command command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);

        if (state.Phase == MatchPhase.MatchOver)
        {
            return IllegalReason.MatchOver;
        }

        if (command.Seat != state.ToPlay)
        {
            return IllegalReason.NotYourTurn;
        }

        return command switch
        {
            Declare declare => ApplyDeclare(state, declare),
            PlayCard play => ApplyPlay(state, play),
            _ => IllegalReason.UnknownCommand,
        };
    }

    public static IReadOnlyList<Command> LegalCommands(MatchState state, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Phase == MatchPhase.MatchOver || seat != state.ToPlay)
        {
            return [];
        }

        var commands = new List<Command>();
        if (DeclarableNow(state, seat).Points > 0)
        {
            commands.Add(new Declare(seat));
        }

        foreach (var card in state.HandOf(seat))
        {
            var options = Captures.Options(card, state.Table);
            if (options.IsEmpty)
            {
                commands.Add(new PlayCard(seat, card));
            }
            else
            {
                commands.AddRange(options.Select(o => new PlayCard(seat, card, o)));
            }
        }

        return commands;
    }

    public static PlayerView ViewFor(MatchState state, Seat seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new PlayerView(
            Seat: seat,
            Phase: state.Phase,
            MatchNumber: state.MatchNumber,
            RoundNumber: state.RoundNumber,
            Dealer: state.Dealer,
            ToPlay: state.ToPlay,
            DealNumber: state.DealNumber,
            PlaysInRound: state.PlaysInRound,
            Hand: state.HandOf(seat),
            Table: state.Table,
            DeckCount: state.Deck.Length,
            HandCounts: [.. state.Hands.Select(h => h.Length)],
            Score: state.Score,
            Declarations: state.Declarations,
            AvailableDeclaration: state.Phase == MatchPhase.AwaitingPlay && seat == state.ToPlay
                ? DeclarableNow(state, seat)
                : DeclarationValue.None,
            Winner: state.Winner);
    }

    /// <summary>A3: only on your turn, before your first card of the deal, once per deal.</summary>
    private static DeclarationValue DeclarableNow(MatchState state, Seat seat) =>
        state.PlayedThisDeal[seat.Index] || state.HasDeclaredThisDeal(seat)
            ? DeclarationValue.None
            : Declarations.Evaluate(state.HandOf(seat));

    private static Result<Transition> ApplyDeclare(MatchState state, Declare command)
    {
        var seat = command.Seat;
        if (state.PlayedThisDeal[seat.Index])
        {
            return IllegalReason.AlreadyPlayedThisDeal;
        }

        if (state.HasDeclaredThisDeal(seat))
        {
            return IllegalReason.AlreadyDeclared;
        }

        var value = Declarations.Evaluate(state.HandOf(seat));
        if (value.Points == 0)
        {
            return IllegalReason.NothingToDeclare;
        }

        var next = state with
        {
            Score = state.Score.Add(seat.Team, value.Points),
            Ledger = state.Ledger with { Declarations = state.Ledger.Declarations.Add(seat.Team, value.Points) },
            Declarations = state.Declarations.Add(new DeclarationRecord(seat, state.DealNumber, value.Kind, value.Points)),
            Sequence = state.Sequence + 1,
        };
        return new Transition(next, [new Declared(seat, value.Kind, value.Points)]);
    }

    private static Result<Transition> ApplyPlay(MatchState state, PlayCard command)
    {
        var seat = command.Seat;
        var hand = state.HandOf(seat);
        if (!hand.Contains(command.Card))
        {
            return IllegalReason.CardNotInHand;
        }

        var options = Captures.Options(command.Card, state.Table);
        if (options.IsEmpty && command.Capture is not null)
        {
            return IllegalReason.CaptureNotAllowed;
        }

        if (!options.IsEmpty && command.Capture is null)
        {
            return IllegalReason.MustCapture; // P4
        }

        if (command.Capture is not null && !options.Contains(command.Capture))
        {
            return IllegalReason.InvalidCapture;
        }

        var events = ImmutableArray.CreateBuilder<GameEvent>();
        int plays = state.PlaysInRound + 1;
        bool isLastPlay = plays == MatchState.PlaysPerRound;

        var next = state with
        {
            Hands = state.Hands.SetItem(seat.Index, hand.Remove(command.Card)),
            PlaysInRound = plays,
            PlayedThisDeal = state.PlayedThisDeal.SetItem(seat.Index, true),
            ToPlay = seat.Next(),
            Sequence = state.Sequence + 1,
        };

        if (command.Capture is null)
        {
            next = next with { Table = state.Table.Add(command.Card) };
            events.Add(new CardPlayed(seat, command.Card, [], IsSweep: false));
        }
        else
        {
            var captured = command.Capture.Cards;
            var table = state.Table.RemoveRange(captured);
            var team = seat.Team;
            bool isSweep = table.IsEmpty && !isLastPlay; // P6
            next = next with
            {
                Table = table,
                Piles = state.Piles.SetItem((int)team, state.PileOf(team).AddRange(captured).Add(command.Card)),
                LastCapturingTeam = team,
                Score = isSweep ? next.Score.Add(team, 1) : next.Score,
                Ledger = isSweep ? next.Ledger with { Sweeps = next.Ledger.Sweeps.Add(team, 1) } : next.Ledger,
            };
            events.Add(new CardPlayed(seat, command.Card, captured, isSweep));
        }

        if (next.Hands.All(h => h.IsEmpty))
        {
            next = next.Deck.IsEmpty ? EndRound(next, events) : DealNext(next, events);
        }

        return new Transition(next, events.ToImmutable());
    }

    private static MatchState StartRound(
        Pcg32 rng,
        Seat dealer,
        int matchNumber,
        int roundNumber,
        TeamScores score,
        int sequence,
        ImmutableArray<GameEvent>.Builder events)
    {
        events.Add(new RoundStarted(matchNumber, roundNumber, dealer));

        Card[] deck;
        while (true)
        {
            deck = rng.Shuffle(Card.FullDeck);
            var table = deck.AsSpan(Seat.Count * MatchState.CardsPerHand, MatchState.InitialTableCards).ToArray();
            if (table.Count(c => c.Rank == Rank.Ace) < 2)
            {
                break;
            }

            events.Add(new DeckReshuffled([.. table])); // S5, C8
        }

        var hands = new ImmutableArray<Card>[Seat.Count];
        int position = 0;
        foreach (var seat in DealOrder(dealer))
        {
            hands[seat.Index] = [.. deck.AsSpan(position, MatchState.CardsPerHand)];
            position += MatchState.CardsPerHand;
        }

        ImmutableArray<Card> initialTable = [.. deck.AsSpan(position, MatchState.InitialTableCards)];
        position += MatchState.InitialTableCards;

        var state = new MatchState
        {
            Rng = rng,
            Phase = MatchPhase.AwaitingPlay,
            MatchNumber = matchNumber,
            RoundNumber = roundNumber,
            Dealer = dealer,
            ToPlay = dealer.Next(), // S6
            DealNumber = 1,
            PlaysInRound = 0,
            Hands = [.. hands],
            Table = initialTable,
            Deck = [.. deck.AsSpan(position)],
            Piles = [[], []],
            Unclaimed = [],
            LastCapturingTeam = null,
            Score = score,
            Ledger = RoundLedger.Empty,
            PlayedThisDeal = [false, false, false, false],
            Declarations = [],
            Winner = null,
            Sequence = sequence,
        };

        AddDealEvents(state, events);
        return state;
    }

    private static MatchState DealNext(MatchState state, ImmutableArray<GameEvent>.Builder events)
    {
        var hands = state.Hands.ToBuilder();
        int position = 0;
        foreach (var seat in DealOrder(state.Dealer))
        {
            hands[seat.Index] = [.. state.Deck.AsSpan(position, MatchState.CardsPerHand)];
            position += MatchState.CardsPerHand;
        }

        var next = state with
        {
            Hands = hands.MoveToImmutable(),
            Deck = [.. state.Deck.AsSpan()[position..]],
            DealNumber = state.DealNumber + 1,
            PlayedThisDeal = [false, false, false, false],
        };
        AddDealEvents(next, events);
        return next;
    }

    private static void AddDealEvents(MatchState state, ImmutableArray<GameEvent>.Builder events)
    {
        foreach (var seat in Seat.All)
        {
            events.Add(new HandDealt(seat, state.HandOf(seat)));
        }

        events.Add(new DealStarted(state.DealNumber, state.Table, state.Deck.Length));
    }

    private static MatchState EndRound(MatchState state, ImmutableArray<GameEvent>.Builder events)
    {
        // P7 / C6 / C7: leftover table cards go to the last capturing team, or to nobody.
        if (!state.Table.IsEmpty)
        {
            var leftover = state.Table;
            state = state.LastCapturingTeam is { } last
                ? state with { Piles = state.Piles.SetItem((int)last, state.PileOf(last).AddRange(leftover)) }
                : state with { Unclaimed = state.Unclaimed.AddRange(leftover) };
            state = state with { Table = [] };
            events.Add(new TableAwarded(state.LastCapturingTeam, leftover));
        }

        var roundScore = RoundScoring.Score(state.PileOf(Team.A), state.PileOf(Team.B));
        var score = state.Score.Add(roundScore.Totals);
        events.Add(new RoundScored(roundScore, state.Ledger, score));

        var outcome = MatchRules.Evaluate(score, roundScore.AllCoinsTeam);
        switch (outcome.Kind)
        {
            case MatchOutcomeKind.Win:
                events.Add(new MatchEnded(outcome.Winner!.Value, outcome.Reason!.Value, score));
                return state with { Score = score, Phase = MatchPhase.MatchOver, Winner = outcome.Winner };

            case MatchOutcomeKind.Tiebreak:
                events.Add(new TiebreakStarted(state.MatchNumber + 1));
                return StartRound(
                    state.Rng, state.Dealer.Next(), state.MatchNumber + 1, roundNumber: 1, TeamScores.Zero, state.Sequence, events);

            default:
                return StartRound(
                    state.Rng, state.Dealer.Next(), state.MatchNumber, state.RoundNumber + 1, score, state.Sequence, events);
        }
    }

    /// <summary>Cards are dealt starting from the seat after the dealer.</summary>
    /// <remarks>An array, not <c>yield</c>: compiler-generated iterators touch <c>System.Environment</c>.</remarks>
    private static Seat[] DealOrder(Seat dealer)
    {
        var order = new Seat[Seat.Count];
        var seat = dealer.Next();
        for (int i = 0; i < Seat.Count; i++)
        {
            order[i] = seat;
            seat = seat.Next();
        }

        return order;
    }
}
