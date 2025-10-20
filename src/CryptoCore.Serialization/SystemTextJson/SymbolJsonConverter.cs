using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoCore.Primitives;

namespace CryptoCore.Serialization.SystemTextJson;

/// <summary>
/// System.Text.Json converter for <see cref="Symbol"/> that serializes to a single string
/// (using <see cref="Symbol.ToString()"/>) and deserializes from any string accepted by
/// <see cref="Symbol.Parse(string)"/>. JSON null produces <c>default(Symbol)</c>.
/// </summary>
public sealed class SymbolJsonConverter : JsonConverter<Symbol>
{
    /// <summary>
    /// Reads a JSON string and parses it into a <see cref="Symbol"/>. Returns <c>default</c> on JSON null.
    /// Throws <see cref="JsonException"/> if the token is not a string or parsing fails.
    /// </summary>
    public override Symbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string for Symbol.");

        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return default;

        if (!Symbol.TryParse(s.AsSpan(), out var symbol))
            throw new JsonException($"Invalid Symbol: '{s}'.");

        return symbol;
    }

    /// <summary>
    /// Writes the <see cref="Symbol"/> as a JSON string using its exchange-native <see cref="Symbol.ToString()"/>.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, Symbol value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    /// <inheritdoc />
    public override Symbol ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Reader at PropertyName or String — parse the key the same way as a value
        var name = reader.GetString();
        if (string.IsNullOrEmpty(name))
            throw new JsonException("Symbol property name is empty.");

        if (!Symbol.TryParse(name.AsSpan(), out var sym))
            throw new JsonException($"Invalid symbol property name: '{name}'.");

        return sym;
    }

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Symbol value, JsonSerializerOptions options)
    {
        // Use native symbol string for dictionary keys
        writer.WritePropertyName(value.ToString());
    }
}
