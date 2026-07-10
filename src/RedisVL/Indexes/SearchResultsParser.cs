using RedisVL.Queries;
using StackExchange.Redis;

namespace RedisVL.Indexes;

internal static class SearchResultsParser
{
    public static SearchResults Parse(RedisResult result)
    {
        if (result.IsNull)
        {
            return new SearchResults(0, []);
        }

        if (SearchReplyReader.IsMapReply(result))
        {
            return ParseMapReply(result);
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
            var values = ParseValues(entries[index + 1], "Search result field name cannot be null.");
            documents.Add(new SearchDocument(id, values));
        }

        return new SearchResults(totalCount, documents);
    }

    // RESP3 FT.SEARCH replies are maps ({ total_results, results: [{ id, extra_attributes }, ...] })
    // rather than the flat RESP2 array. See SearchReplyReader for the shape details.
    private static SearchResults ParseMapReply(RedisResult result)
    {
        var map = SearchReplyReader.ToMap(result);
        var totalCount = SearchReplyReader.ReadTotalCount(map);
        var rows = SearchReplyReader.ReadRows(map);

        var documents = new List<SearchDocument>(rows.Length);
        foreach (var row in rows)
        {
            var rowMap = SearchReplyReader.ToMap(row);
            var id = rowMap.TryGetValue(SearchReplyReader.IdKey, out var idValue)
                ? idValue.ToString() ?? throw new InvalidOperationException("Search result document id cannot be null.")
                : throw new InvalidOperationException("Search result is missing the document id.");
            var values = rowMap.TryGetValue(SearchReplyReader.ExtraAttributesKey, out var attributes)
                ? ParseValues(attributes, "Search result field name cannot be null.")
                : new Dictionary<string, RedisValue>(StringComparer.Ordinal);
            documents.Add(new SearchDocument(id, values));
        }

        return new SearchResults(totalCount, documents);
    }

    internal static IReadOnlyDictionary<string, RedisValue> ParseValues(RedisResult result, string nullFieldNameMessage)
    {
        if (result.IsNull)
        {
            return new Dictionary<string, RedisValue>(StringComparer.Ordinal);
        }

        var entries = (RedisResult[])result!;
        var values = new Dictionary<string, RedisValue>(entries.Length / 2, StringComparer.Ordinal);
        for (var index = 0; index < entries.Length; index += 2)
        {
            var key = entries[index].ToString() ?? throw new InvalidOperationException(nullFieldNameMessage);
            values[key] = (RedisValue)entries[index + 1];
        }

        return values;
    }
}
