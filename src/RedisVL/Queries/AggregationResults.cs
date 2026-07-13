using StackExchange.Redis;
using System.Text.Json;

namespace RedisVL.Queries;

/// <summary>
/// Represents the untyped result set of an <c>FT.AGGREGATE</c> pipeline, exposing the reported
/// total count and the sequence of returned rows.
/// </summary>
public sealed class AggregationResults
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationResults"/> class.
    /// </summary>
    /// <param name="totalCount">The count reported at the head of the <c>FT.AGGREGATE</c> reply.</param>
    /// <param name="rows">The rows returned by the aggregation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="totalCount"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// The count reported at the head of the <c>FT.AGGREGATE</c> reply (the leading array element
    /// over RESP2, the <c>total_results</c> map entry over RESP3). This value is <b>not reliable</b>
    /// and varies by pipeline, protocol, and Redis version: for non-GROUPBY (LOAD/APPLY-only)
    /// pipelines Redis returns <c>1</c> rather than the matching-row count, and for GROUPBY pipelines
    /// it may report either the number of groups or the number of matched input records. Use
    /// <see cref="Rows"/>'s <c>Count</c> for the
    /// number of rows actually returned.
    /// </summary>
    public long TotalCount { get; }

    /// <summary>
    /// Gets the rows returned by the aggregation, in the order Redis produced them.
    /// </summary>
    public IReadOnlyList<AggregationResultRow> Rows { get; }

    /// <summary>
    /// Projects each row onto <typeparamref name="TDocument"/>, producing a strongly typed result set.
    /// </summary>
    /// <typeparam name="TDocument">The type to map each row to.</typeparam>
    /// <param name="serializerOptions">Optional JSON serializer options used when mapping fields.</param>
    /// <returns>A typed <see cref="AggregationResults{TDocument}"/> carrying the mapped rows.</returns>
    public AggregationResults<TDocument> Map<TDocument>(JsonSerializerOptions? serializerOptions = null)
    {
        var mappedRows = Rows.Select(row => row.Map<TDocument>(serializerOptions)).ToArray();
        return new AggregationResults<TDocument>(TotalCount, mappedRows);
    }
}

/// <summary>
/// Represents a single row of an <c>FT.AGGREGATE</c> reply as a set of field name/value pairs.
/// </summary>
public sealed class AggregationResultRow
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationResultRow"/> class.
    /// </summary>
    /// <param name="values">The field name to value mapping for this row.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    public AggregationResultRow(IReadOnlyDictionary<string, RedisValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = values;
    }

    /// <summary>
    /// Gets the field name to value mapping for this row.
    /// </summary>
    public IReadOnlyDictionary<string, RedisValue> Values { get; }

    /// <summary>
    /// Maps this row's fields onto an instance of <typeparamref name="TDocument"/>.
    /// </summary>
    /// <typeparam name="TDocument">The type to map the row to.</typeparam>
    /// <param name="serializerOptions">Optional JSON serializer options used when mapping fields.</param>
    /// <returns>The mapped document.</returns>
    public TDocument Map<TDocument>(JsonSerializerOptions? serializerOptions = null) =>
        SearchResultMapper.Map<TDocument>(Values, documentId: null, serializerOptions);

    /// <summary>
    /// Attempts to retrieve the value of the named field.
    /// </summary>
    /// <param name="fieldName">The field name to look up; a leading <c>@</c> is not required.</param>
    /// <param name="value">When this method returns, contains the field value if found; otherwise the default value.</param>
    /// <returns><see langword="true"/> if the field was present; otherwise <see langword="false"/>.</returns>
    public bool TryGetValue(string fieldName, out RedisValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);
        return Values.TryGetValue(fieldName.Trim(), out value);
    }
}

/// <summary>
/// Represents the strongly typed result set of an <c>FT.AGGREGATE</c> pipeline whose rows have been
/// mapped onto <typeparamref name="TDocument"/>.
/// </summary>
/// <typeparam name="TDocument">The type each aggregation row is mapped to.</typeparam>
public sealed class AggregationResults<TDocument>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationResults{TDocument}"/> class.
    /// </summary>
    /// <param name="totalCount">The count reported at the head of the <c>FT.AGGREGATE</c> reply.</param>
    /// <param name="rows">The mapped rows returned by the aggregation.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="totalCount"/> is negative.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is <see langword="null"/>.</exception>
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

    /// <summary>
    /// The count reported at the head of the <c>FT.AGGREGATE</c> reply (the leading array element
    /// over RESP2, the <c>total_results</c> map entry over RESP3). This value is <b>not reliable</b>
    /// and varies by pipeline, protocol, and Redis version: for non-GROUPBY (LOAD/APPLY-only)
    /// pipelines Redis returns <c>1</c> rather than the matching-row count, and for GROUPBY pipelines
    /// it may report either the number of groups or the number of matched input records. Use
    /// <see cref="Rows"/>'s <c>Count</c> for the
    /// number of rows actually returned.
    /// </summary>
    public long TotalCount { get; }

    /// <summary>
    /// Gets the mapped rows returned by the aggregation, in the order Redis produced them.
    /// </summary>
    public IReadOnlyList<TDocument> Rows { get; }
}
