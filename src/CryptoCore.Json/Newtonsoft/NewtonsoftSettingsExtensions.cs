using Newtonsoft.Json;

namespace CryptoCore.Json.Newtonsoft;

/// <summary>
/// Helper extensions to register CryptoCore converters with <see cref="JsonSerializerSettings"/>.
/// </summary>
public static class NewtonsoftSettingsExtensions
{
    /// <summary>
    /// Adds CryptoCore converters (currently <c>SymbolNewtonsoftConverter</c>) to the provided settings.
    /// </summary>
    public static JsonSerializerSettings AddCryptoCoreConverters(this JsonSerializerSettings settings)
    {
        settings.Converters.Add(new SymbolNewtonsoftConverter());
        settings.Converters.Add(new AssetNewtonsoftConverter());
        settings.Converters.Add(new ExchangeNewtonsoftConverter());
        return settings;
    }
}
