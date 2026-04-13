using StackExchange.Redis;

namespace RedisVlDotNet.Indexes;

public sealed class SearchIndexListItem
{
    public SearchIndexListItem(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

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
