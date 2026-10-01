using Bunit;
using Spincio.Client.Components;
using Spincio.Engine;

namespace Spincio.Client.Tests;

/// <summary>ADR 0011: every card has its image, named with the card notation.</summary>
public class CardImagesTests : BunitContext
{
    private static string CardsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Spincio.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "src", "Spincio.Client", "wwwroot", "cards");
    }

    [Fact]
    public void There_is_one_image_per_card_and_nothing_else()
    {
        var expected = Card.FullDeck.Select(c => $"{c}.webp").Order(StringComparer.Ordinal).ToList();
        var actual = Directory.GetFiles(CardsFolder()).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToList();

        actual.ShouldBe(expected);
    }

    [Fact]
    public void Card_face_points_to_the_card_image()
    {
        var face = Render<CardFace>(p => p.Add(c => c.Card, Card.Parse("KB")));

        face.Find("img").GetAttribute("src").ShouldBe("cards/KB.webp");
    }
}
