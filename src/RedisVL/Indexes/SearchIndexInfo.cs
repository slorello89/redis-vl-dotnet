using StackExchange.Redis;

namespace RedisVL.Indexes;

/// <summary>Exposes the metadata returned by the <c>FT.INFO</c> command for a search index.</summary>
public sealed class SearchIndexInfo
{
    /// <summary>Initializes a new instance of the <see cref="SearchIndexInfo"/> class.</summary>
    /// <param name="attributes">The raw attribute name/value pairs parsed from the <c>FT.INFO</c> response.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="attributes"/> is <see langword="null"/>.</exception>
    public SearchIndexInfo(IReadOnlyDictionary<string, RedisResult> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        Attributes = attributes;
    }

    /// <summary>Gets the raw attribute name/value pairs reported by <c>FT.INFO</c>.</summary>
    public IReadOnlyDictionary<string, RedisResult> Attributes { get; }

    /// <summary>Gets the name of the index, read from the <c>index_name</c> attribute.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the <c>FT.INFO</c> response did not include <c>index_name</c>.</exception>
    public string Name => GetString("index_name")
        ?? throw new InvalidOperationException("Redis FT.INFO response did not include index_name.");

    /// <summary>Gets the string value of an attribute, or <see langword="null"/> when it is absent.</summary>
    /// <param name="attributeName">The name of the attribute to read.</param>
    /// <returns>The attribute value as a string, or <see langword="null"/> if not present.</returns>
    public string? GetString(string attributeName) =>
        TryGetValue(attributeName, out var value) ? value.ToString() : null;

    /// <summary>Attempts to get the raw value of an attribute.</summary>
    /// <param name="attributeName">The name of the attribute to read.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the attribute value.</param>
    /// <returns><see langword="true"/> if the attribute exists and has a non-null value; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="attributeName"/> is <see langword="null"/>, empty, or whitespace.</exception>
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
