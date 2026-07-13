using StackExchange.Redis;

namespace RedisVL.Indexes;

/// <summary>Represents a single index entry returned by the <c>FT._LIST</c> command.</summary>
public sealed class SearchIndexListItem
{
    /// <summary>Initializes a new instance of the <see cref="SearchIndexListItem"/> class.</summary>
    /// <param name="name">The index name; leading and trailing whitespace is trimmed.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public SearchIndexListItem(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>Gets the name of the index.</summary>
    public string Name { get; }

    internal static IReadOnlyList<SearchIndexListItem> FromRedisResult(RedisResult result)
    {
        var entries = (RedisResult[])result!;
        var items = new List<SearchIndexListItem>(entries.Length);

        foreach (var entry in entries)
        {
            var name = entry.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Redis FT._LIST response contained an empty index name.");
            }

            items.Add(new SearchIndexListItem(name));
        }

        return items;
    }
}
