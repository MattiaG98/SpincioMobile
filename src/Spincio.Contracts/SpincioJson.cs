using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Spincio.Engine;

namespace Spincio.Contracts;

/// <summary>
/// JSON settings shared by server and client, so engine types travel over SignalR unchanged:
/// <see cref="GameEvent"/> is polymorphic (discriminator <c>$kind</c>) and <see cref="Seat"/> is a plain number.
/// The engine itself stays free of serialization attributes.
/// </summary>
public static class SpincioJson
{
    /// <summary>Every concrete event type; a test checks the list against the engine assembly.</summary>
    public static IReadOnlyList<Type> EventTypes { get; } =
    [
        typeof(RoundStarted),
        typeof(DeckReshuffled),
        typeof(HandDealt),
        typeof(DealStarted),
        typeof(Declared),
        typeof(CardPlayed),
        typeof(TableAwarded),
        typeof(RoundScored),
        typeof(TiebreakStarted),
        typeof(MatchEnded),
    ];

    public static JsonSerializerOptions Options { get; } = Create();

    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Configure(options);
        return options;
    }

    public static void Configure(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Converters.Add(new SeatConverter());
        options.TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AddEventPolymorphism } };
    }

    private static void AddEventPolymorphism(JsonTypeInfo info)
    {
        if (info.Type != typeof(GameEvent))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$kind",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        foreach (var type in EventTypes)
        {
            info.PolymorphismOptions.DerivedTypes.Add(new JsonDerivedType(type, type.Name));
        }
    }

    private sealed class SeatConverter : JsonConverter<Seat>
    {
        public override Seat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetInt32());

        public override void Write(Utf8JsonWriter writer, Seat value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Index);
    }
}
