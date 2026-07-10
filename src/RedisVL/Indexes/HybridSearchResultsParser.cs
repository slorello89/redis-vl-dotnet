using RedisVL.Queries;
using StackExchange.Redis;

namespace RedisVL.Indexes;

/// <summary>
/// Parses the reply of the <c>FT.HYBRID</c> command into <see cref="SearchResults" />.
/// </summary>
/// <remarks>
/// The reply is a map with <c>total_results</c>, <c>results</c>, <c>warnings</c>, and
/// <c>execution_time</c> entries — an alternating key/value array over RESP2 and a native map over
/// RESP3, both of which flatten to the same key/value pairs. Each row in <c>results</c> is a flat
/// field/value collection that includes the document key (<see cref="HybridSearchQuery.KeyField" />)
/// and fused score (<see cref="HybridSearchQuery.ScoreField" />).
/// </remarks>
internal static class HybridSearchResultsParser
{
    public static SearchResults Parse(RedisResult result)
    {
        if (result.IsNull)
        {
            return new SearchResults(0, []);
        }

        var entries = (RedisResult[])result!;
        long totalCount = 0;
        var documents = new List<SearchDocument>();

        for (var index = 0; index + 1 < entries.Length; index += 2)
        {
            var key = entries[index].ToString();
            var value = entries[index + 1];

            switch (key)
            {
                case "total_results":
                    totalCount = (long)value;
                    break;
                case "results":
                    documents.AddRange(ParseRows(value));
                    break;
            }
        }

        return new SearchResults(totalCount, documents);
    }

    private static IEnumerable<SearchDocument> ParseRows(RedisResult results)
    {
        if (results.IsNull)
        {
            yield break;
        }

        foreach (var row in (RedisResult[])results!)
        {
            // FT.HYBRID rows are a flat field/value collection on both protocols — an array over
            // RESP2 and a map (which flattens to the same key/value pairs) over RESP3 — so the row
            // itself is the field list; there is no extra_attributes envelope to unwrap.
            var fields = SearchResultsParser.ParseValues(row, "Hybrid search result field name cannot be null.");

            if (!fields.TryGetValue(HybridSearchQuery.KeyField, out var documentKey))
            {
                throw new InvalidOperationException(
                    $"Hybrid search result is missing the '{HybridSearchQuery.KeyField}' field required to identify the document.");
            }

            var id = documentKey.ToString()
                ?? throw new InvalidOperationException("Hybrid search result document id cannot be null.");

            var values = new Dictionary<string, RedisValue>(fields.Count, StringComparer.Ordinal);
            foreach (var pair in fields)
            {
                if (string.Equals(pair.Key, HybridSearchQuery.KeyField, StringComparison.Ordinal))
                {
                    continue;
                }

                values[pair.Key] = pair.Value;
            }

            yield return new SearchDocument(id, values);
        }
    }
}
