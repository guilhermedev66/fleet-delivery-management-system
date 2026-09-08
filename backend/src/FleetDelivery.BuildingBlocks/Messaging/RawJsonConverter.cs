using System.Text.Json;
using System.Text.Json.Serialization;

namespace FleetDelivery.BuildingBlocks.Messaging;

/// <summary>
/// Embeds an already-serialized JSON string (e.g. <see cref="OutboxMessage.Content"/>)
/// as a raw nested JSON value rather than re-escaping it as a string — used
/// by <see cref="IntegrationEventEnvelope.Data"/> so a consumer sees
/// <c>"data": { ... }</c>, not <c>"data": "{ ... }"</c>.
/// </summary>
public sealed class RawJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        return document.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        using var document = JsonDocument.Parse(value);
        document.RootElement.WriteTo(writer);
    }
}
