using CryptoCore.Primitives;
using Newtonsoft.Json;

namespace CryptoCore.Serialization.Newtonsoft;

/// <summary>
/// Newtonsoft.Json converter for <see cref="Asset"/> that serializes to a single string
/// and deserializes from strings accepted by <see cref="Asset.TryParse(ReadOnlySpan{char}, out Asset)"/>.
/// JSON null → <c>default(Asset)</c>.
/// </summary>
public sealed class AssetNewtonsoftConverter : JsonConverter<Asset>
{
    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override Asset ReadJson(JsonReader reader, Type objectType, Asset existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return default;

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("Expected string for Asset.");

        var s = (string?)reader.Value;
        if (string.IsNullOrWhiteSpace(s))
            return default;

        if (!Asset.TryParse(s.AsSpan(), out var asset))
            throw new JsonSerializationException($"Invalid Asset: '{s}'.");

        return asset;
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, Asset value, JsonSerializer serializer)
    {
        writer.WriteValue(value.ToString());
    }
}
