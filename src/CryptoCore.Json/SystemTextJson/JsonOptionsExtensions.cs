using System.Text.Json;

namespace CryptoCore.Json.SystemTextJson;

/// <summary>
/// Helper extensions to register CryptoCore converters with <see cref="JsonSerializerOptions"/>.
/// </summary>
public static class JsonOptionsExtensions
{
    /// <summary>
    /// Adds CryptoCore converters (currently <c>SymbolJsonConverter</c>) to the provided options.
    /// </summary>
    public static JsonSerializerOptions AddCryptoCoreConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new SymbolJsonConverter());
        options.Converters.Add(new AssetJsonConverter());
        options.Converters.Add(new ExchangeJsonConverter());
        return options;
    }
}
