using CryptoCore.Root;
using Newtonsoft.Json;

namespace CryptoCore.Json.Newtonsoft;

/// <summary>
/// Newtonsoft.Json converter for <see cref="Exchange"/> that serializes to preset names when possible
/// (e.g., "BinanceFutures") or to a lower-case slug otherwise; and deserializes from either form.
/// JSON null → <c>default(Exchange)</c>.
/// </summary>
public sealed class ExchangeNewtonsoftConverter : JsonConverter
{
    /// <summary>Returns true when the given type is <see cref="Exchange"/>.</summary>
    public override bool CanConvert(Type objectType) => objectType == typeof(Exchange);

    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return default(Exchange);

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("Expected string for Exchange.");

        var s = (string?)reader.Value;
        if (string.IsNullOrWhiteSpace(s))
            return default(Exchange);

        if (SystemTextJson.ExchangeJsonConverter.TryParsePreset(s, out var ex) ||
            SystemTextJson.ExchangeJsonConverter.TryParseSlug(s.AsSpan(), out ex))
            return ex;

        throw new JsonSerializationException($"Invalid Exchange string: '{s}'.");
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var ex = value is Exchange e ? e : default;

        if (SystemTextJson.ExchangeJsonConverter.TryGetPreset(ex, out var preset))
        {
            writer.WriteValue(preset);
            return;
        }

        var slug = SystemTextJson.ExchangeJsonConverter.BuildSlug(ex);
        writer.WriteValue(slug);
    }
}
