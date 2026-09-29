using Spincio.Engine;

namespace Spincio.Engine.Tests;

public class Pcg32Tests
{
    [Fact]
    public void Matches_the_reference_pcg32_demo_vector()
    {
        // pcg32-demo: pcg32_srandom_r(&rng, 42u, 54u); first six outputs.
        var rng = new Pcg32(42, 54);
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];

        var actual = expected.Select(_ => rng.NextUInt()).ToArray();

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Bounded_values_stay_in_range_and_cover_it()
    {
        var rng = Pcg32.FromSeed(1);
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            int v = rng.NextInt(7);
            v.ShouldBeInRange(0, 6);
            seen.Add(v);
        }

        seen.Count.ShouldBe(7);
    }

    [Fact]
    public void Shuffle_is_a_permutation_and_is_deterministic()
    {
        var a = Pcg32.FromSeed(99);
        var b = Pcg32.FromSeed(99);

        var first = a.Shuffle(Card.FullDeck);
        var second = b.Shuffle(Card.FullDeck);

        first.ShouldBe(second);
        first.Order().ShouldBe(Card.FullDeck);
        first.ShouldNotBe(Card.FullDeck);
    }
}

public class CardNotationTests
{
    [Fact]
    public void Every_card_round_trips_through_its_notation()
    {
        foreach (var card in Card.FullDeck)
        {
            Card.Parse(card.ToString()).ShouldBe(card);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("8D")]
    [InlineData("KX")]
    [InlineData("10D")]
    [InlineData(null)]
    public void Invalid_notation_is_rejected(string? notation)
    {
        Card.TryParse(notation, out _).ShouldBeFalse();
    }
}
