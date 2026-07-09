using RedisVL.Queries;
using StackExchange.Redis;

namespace RedisVL.Indexes;

internal static class AggregationResultsParser
{
    public static AggregationResults Parse(RedisResult result)
    {
        if (result.IsNull)
        {
            return new AggregationResults(0, []);
        }

        if (SearchReplyReader.IsMapReply(result))
        {
            return ParseMapReply(result);
        }

        var entries = (RedisResult[])result!;
        if (entries.Length == 0)
        {
            return new AggregationResults(0, []);
        }

        var totalCount = (long)entries[0];
        var rows = new List<AggregationResultRow>(Math.Max(entries.Length - 1, 0));

        for (var index = 1; index < entries.Length; index++)
        {
            rows.Add(new AggregationResultRow(SearchResultsParser.ParseValues(entries[index], "Aggregation result field name cannot be null.")));
        }

        return new AggregationResults(totalCount, rows);
    }

    // RESP3 FT.AGGREGATE replies are maps ({ total_results, results: [{ extra_attributes }, ...] })
    // rather than the flat RESP2 array. See SearchReplyReader for the shape details.
    private static AggregationResults ParseMapReply(RedisResult result)
    {
        var map = SearchReplyReader.ToMap(result);
        var totalCount = SearchReplyReader.ReadTotalCount(map);
        var rows = SearchReplyReader.ReadRows(map);

        var resultRows = new List<AggregationResultRow>(rows.Length);
        foreach (var row in rows)
        {
            var fields = SearchReplyReader.ExtractRowFields(row);
            resultRows.Add(new AggregationResultRow(
                SearchResultsParser.ParseValues(fields, "Aggregation result field name cannot be null.")));
        }

        return new AggregationResults(totalCount, resultRows);
    }
}
