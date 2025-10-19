using CryptoCore.Root;
using Newtonsoft.Json;

namespace CryptoCore.Json.Newtonsoft;

/// <summary>
/// Newtonsoft.Json converter for <see cref="Symbol"/> that serializes to a single string
/// (using <see cref="Symbol.ToString()"/>) and deserializes from any string accepted by
/// <see cref="Symbol.Parse(string)"/>. JSON null produces <c>default(Symbol)</c>.
/// </summary>
public sealed class SymbolNewtonsoftConverter : JsonConverter
{
    /// <summary>
    /// Returns true when the given <paramref name="objectType"/> is <see cref="Symbol"/>.
    /// </summary>
    public override bool CanConvert(Type objectType) => objectType == typeof(Symbol);

    /// <summary>
    /// Reads a JSON value and converts it to <see cref="Symbol"/>. Returns <c>default</c> on JSON null.
    /// Throws <see cref="JsonSerializationException"/> for invalid token types or content.
    /// </summary>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return default(Symbol);

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("Expected string for Symbol.");

        var s = (string?)reader.Value;
        if (string.IsNullOrWhiteSpace(s))
            return default(Symbol);

        if (!Symbol.TryParse(s.AsSpan(), out var symbol))
            throw new JsonSerializationException($"Invalid Symbol: '{s}'.");

        return symbol;
    }

    /// <summary>
    /// Writes the <see cref="Symbol"/> as a JSON string using its exchange-native <see cref="Symbol.ToString()"/>.
    /// </summary>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var symbol = value is Symbol s ? s : default;
        writer.WriteValue(symbol.ToString());
    }
}
