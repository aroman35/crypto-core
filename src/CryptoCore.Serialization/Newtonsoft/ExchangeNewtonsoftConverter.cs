using CryptoCore.Extensions;
using CryptoCore.Primitives;
using Newtonsoft.Json;

namespace CryptoCore.Serialization.Newtonsoft;

/// <summary>
/// Newtonsoft.Json converter for <see cref="Exchange"/>: uses short slugs ("okx-swap") for JSON,
/// and accepts either slugs or enum names on read.
/// </summary>
public sealed class ExchangeNewtonsoftConverter : JsonConverter<Exchange>
{
    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, Exchange value, JsonSerializer serializer)
    {
        var slug = value.ToSlug();
        if (slug.Length == 0)
        {
            throw new JsonSerializationException($"Unsupported exchange flags for JSON: {value}.");
        }

        writer.WriteValue(slug);
    }

    /// <inheritdoc/>
    public override Exchange ReadJson(JsonReader reader, Type objectType, Exchange existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
        {
            return Exchange.None;
        }

        if (reader.TokenType != JsonToken.String)
        {
            throw new JsonSerializationException("Exchange must be a JSON string (slug or enum name).");
        }

        var s = (string)reader.Value!;
        if (!ExchangeExtensions.TryParsePreset(s.AsSpan(), out var x))
        {
            throw new JsonSerializationException($"Invalid exchange: '{s}'.");
        }

        return x;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;
}
