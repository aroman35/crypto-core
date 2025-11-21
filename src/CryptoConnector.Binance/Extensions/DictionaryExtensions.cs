namespace CryptoConnector.Binance.Extensions;

public static class DictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> newValue)
        where TKey : notnull
    {
        if (dictionary.TryGetValue(key, out var value))
            return value;
        value = newValue(key);
        dictionary[key] = value;
        return value;
    }
}
