using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoCore.Extensions;
using CryptoCore.Primitives;

namespace CryptoCore.Serialization.SystemTextJson;

/// <summary>
/// System.Text.Json converter for <see cref="Exchange"/> that serializes to a slug (e.g., "okx-swap-usdm").
/// Supports dictionary keys via <see cref="ReadAsPropertyName"/> and <see cref="WriteAsPropertyName"/>.
/// </summary>
public sealed class ExchangeJsonConverter : JsonConverter<Exchange>
{
    /// <inheritdoc/>
    public override Exchange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Exchange must be a JSON string (slug or enum name).");
        }

        var s = reader.GetString();
        if (string.IsNullOrEmpty(s))
        {
            return Exchange.None;
        }

        if (!ExchangeExtensions.TryParsePreset(s.AsSpan(), out var x))
        {
            throw new JsonException($"Invalid exchange: '{s}'.");
        }

        return x;
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Exchange value, JsonSerializerOptions options)
    {
        var slug = value.ToSlug();
        if (slug.Length == 0)
        {
            throw new JsonException($"Unsupported exchange flags for JSON: {value}.");
        }

        writer.WriteStringValue(slug);
    }

    /// <inheritdoc/>
    public override Exchange ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString();
        if (string.IsNullOrEmpty(s))
        {
            return Exchange.None;
        }

        if (!ExchangeExtensions.TryParsePreset(s.AsSpan(), out var x))
        {
            throw new JsonException($"Invalid exchange property name: '{s}'.");
        }

        return x;
    }

    /// <inheritdoc/>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Exchange value, JsonSerializerOptions options)
    {
        var slug = value.ToSlug();
        if (slug.Length == 0)
        {
            throw new JsonException($"Unsupported exchange flags for JSON property: {value}.");
        }

        writer.WritePropertyName(slug);
    }
}
