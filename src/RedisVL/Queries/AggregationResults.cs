using StackExchange.Redis;
using System.Text.Json;

namespace RedisVL.Queries;

public sealed class AggregationResults
{
    public AggregationResults(long totalCount, IReadOnlyList<AggregationResultRow> rows)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(rows);
        TotalCount = totalCount;
        Rows = rows;
    }

    public long TotalCount { get; }

    public IReadOnlyList<AggregationResultRow> Rows { get; }

    public AggregationResults<TDocument> Map<TDocument>(JsonSerializerOptions? serializerOptions = null)
    {
        var mappedRows = Rows.Select(row => row.Map<TDocument>(serializerOptions)).ToArray();
        return new AggregationResults<TDocument>(TotalCount, mappedRows);
    }
}

public sealed class AggregationResultRow
{
    public AggregationResultRow(IReadOnlyDictionary<string, RedisValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
    }

    public IReadOnlyDictionary<string, RedisValue> Values { get; }

    public TDocument Map<TDocument>(JsonSerializerOptions? serializerOptions = null) =>
        SearchResultMapper.Map<TDocument>(Values, documentId: null, serializerOptions);

    public bool TryGetValue(string fieldName, out RedisValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return Values.TryGetValue(fieldName.Trim(), out value);
    }
}

public sealed class AggregationResults<TDocument>
{
    public AggregationResults(long totalCount, IReadOnlyList<TDocument> rows)
    {
        if (totalCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalCount), totalCount, "Total count cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(rows);
        TotalCount = totalCount;
        Rows = rows;
    }

    public long TotalCount { get; }

    public IReadOnlyList<TDocument> Rows { get; }
}
