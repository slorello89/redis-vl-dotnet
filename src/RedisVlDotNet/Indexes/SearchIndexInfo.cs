using StackExchange.Redis;

namespace RedisVlDotNet.Indexes;

public sealed class SearchIndexInfo
{
    public SearchIndexInfo(IReadOnlyDictionary<string, RedisResult> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        Attributes = attributes;
    }

    public IReadOnlyDictionary<string, RedisResult> Attributes { get; }

    public string Name => GetString("index_name")
        ?? throw new InvalidOperationException("Redis FT.INFO response did not include index_name.");

    public string? GetString(string attributeName) =>
        TryGetValue(attributeName, out var value) ? value.ToString() : null;

    public bool TryGetValue(string attributeName, out RedisResult value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attributeName);
        if (Attributes.TryGetValue(attributeName, out var foundValue) && foundValue is not null)
        {
            value = foundValue;
            return true;
        }

        value = default!;
        return false;
    }

    internal static SearchIndexInfo FromRedisResult(RedisResult result)
    {
        var entries = (RedisResult[])result!;
        if (entries.Length % 2 != 0)
        {
            throw new InvalidOperationException("Redis FT.INFO response must contain key-value pairs.");
        }

        var attributes = new Dictionary<string, RedisResult>(StringComparer.Ordinal);
        for (var index = 0; index < entries.Length; index += 2)
        {
            attributes[(string)entries[index]!] = entries[index + 1];
        }

        return new SearchIndexInfo(attributes);
    }
}
