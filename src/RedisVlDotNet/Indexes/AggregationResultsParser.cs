using RedisVlDotNet.Queries;
using StackExchange.Redis;

namespace RedisVlDotNet.Indexes;

internal static class AggregationResultsParser
{
    public static AggregationResults Parse(RedisResult result)
    {
        if (result.IsNull)
        {
            return new AggregationResults(0, []);
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
}
