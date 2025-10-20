namespace CryptoCore.Serialization.SystemTextJson;

using System.Text.Json;
using System.Text.Json.Serialization;
using Primitives;

/// <summary>
/// System.Text.Json converter for <see cref="Asset"/> that serializes to a single string
/// (using <see cref="Asset.ToString()"/>) and deserializes from any string accepted by
/// <see cref="Asset.TryParse(ReadOnlySpan{char}, out Asset)"/>. JSON null produces <c>default(Asset)</c>.
/// </summary>
public sealed class AssetJsonConverter : JsonConverter<Asset>
{
    /// <summary>
    /// Reads a JSON string and parses it into a <see cref="Asset"/>. Returns <c>default</c> on JSON null.
    /// Throws <see cref="JsonException"/> if the token is not a string or parsing fails.
    /// </summary>
    public override Asset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string for Asset.");

        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return default;

        if (!Asset.TryParse(s.AsSpan(), out var asset))
            throw new JsonException($"Invalid Asset: '{s}'.");

        return asset;
    }

    /// <summary>
    /// Writes the <see cref="Asset"/> as a JSON string using <see cref="Asset.ToString()"/>.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, Asset value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());

    /// <inheritdoc />
    public override Asset ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var name = reader.GetString();
        if (string.IsNullOrEmpty(name))
            throw new JsonException("Asset property name is empty.");

        if (!Asset.TryParse(name.AsSpan(), out var a))
            throw new JsonException($"Invalid asset property name: '{name}'.");

        return a;
    }

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, Asset value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.ToString());
    }
}
