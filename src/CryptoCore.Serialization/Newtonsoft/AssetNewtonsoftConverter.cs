using CryptoCore.Primitives;
using Newtonsoft.Json;

namespace CryptoCore.Serialization.Newtonsoft;

/// <summary>
/// Newtonsoft.Json converter for <see cref="Asset"/> that serializes to a single string
/// and deserializes from strings accepted by <see cref="Asset.TryParse(ReadOnlySpan{char}, out Asset)"/>.
/// JSON null → <c>default(Asset)</c>.
/// </summary>
public sealed class AssetNewtonsoftConverter : JsonConverter
{
    /// <summary>Returns true when the given type is <see cref="Asset"/>.</summary>
    public override bool CanConvert(Type objectType) => objectType == typeof(Asset);

    /// <inheritdoc/>
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return default(Asset);

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("Expected string for Asset.");

        var s = (string?)reader.Value;
        if (string.IsNullOrWhiteSpace(s))
            return default(Asset);

        if (!Asset.TryParse(s.AsSpan(), out var asset))
            throw new JsonSerializationException($"Invalid Asset: '{s}'.");

        return asset;
    }

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var asset = value is Asset a ? a : default;
        writer.WriteValue(asset.ToString());
    }
}
