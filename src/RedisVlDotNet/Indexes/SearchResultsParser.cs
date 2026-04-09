using RedisVlDotNet.Queries;
using StackExchange.Redis;

namespace RedisVlDotNet.Indexes;

internal static class SearchResultsParser
{
    public static SearchResults Parse(RedisResult result)
    {
        if (result.IsNull)
        {
            return new SearchResults(0, []);
        }

        var entries = (RedisResult[])result!;
        if (entries.Length == 0)
        {
            return new SearchResults(0, []);
        }

        var totalCount = (long)entries[0];
        var documents = new List<SearchDocument>();

        for (var index = 1; index < entries.Length; index += 2)
        {
            var id = entries[index].ToString() ?? throw new InvalidOperationException("Search result document id cannot be null.");
            var values = ParseValues(entries[index + 1]);
            documents.Add(new SearchDocument(id, values));
        }

        return new SearchResults(totalCount, documents);
    }

    private static IReadOnlyDictionary<string, RedisValue> ParseValues(RedisResult result)
    {
        if (result.IsNull)
        {
            return new Dictionary<string, RedisValue>(StringComparer.Ordinal);
        }

        var entries = (RedisResult[])result!;
        var values = new Dictionary<string, RedisValue>(entries.Length / 2, StringComparer.Ordinal);
        for (var index = 0; index < entries.Length; index += 2)
        {
            var key = entries[index].ToString() ?? throw new InvalidOperationException("Search result field name cannot be null.");
            values[key] = (RedisValue)entries[index + 1];
        }

        return values;
    }
}
