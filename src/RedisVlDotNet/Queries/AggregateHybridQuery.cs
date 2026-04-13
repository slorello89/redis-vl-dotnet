using System.Runtime.InteropServices;
using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

public sealed class AggregateHybridQuery
{
    public AggregateHybridQuery(
        FilterExpression textFilter,
        string vectorFieldName,
        byte[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? loadFields = null,
        IEnumerable<AggregationApply>? applyClauses = null,
        AggregationGroupBy? groupBy = null,
        AggregationSortBy? sortBy = null,
        int offset = 0,
        int limit = 10,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null)
    {
        ArgumentNullException.ThrowIfNull(textFilter);
        ArgumentException.ThrowIfNullOrWhiteSpace(vectorFieldName);
        ArgumentNullException.ThrowIfNull(vector);
        ArgumentException.ThrowIfNullOrWhiteSpace(scoreAlias);

        if (vector.Length == 0)
        {
            throw new ArgumentException("Vector input must contain at least one byte.", nameof(vector));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, "TopK must be greater than zero.");
        }

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit cannot be negative.");
        }

        if (!QueryFilterInspector.ContainsTextExpression(textFilter))
        {
            throw new ArgumentException("Aggregate hybrid queries require at least one text predicate in the text filter.", nameof(textFilter));
        }

        TextFilter = textFilter;
        VectorFieldName = FilterExpression.NormalizeFieldName(vectorFieldName);
        Vector = vector.ToArray();
        TopK = topK;
        Filter = filter;
        LoadFields = NormalizeFields(loadFields);
        ApplyClauses = applyClauses?.ToArray() ?? [];
        GroupBy = groupBy;
        SortBy = sortBy;
        Offset = offset;
        Limit = limit;
        ScoreAlias = FilterExpression.NormalizeFieldName(scoreAlias);
        RuntimeOptions = runtimeOptions;
    }

    public FilterExpression TextFilter { get; }

    public string VectorFieldName { get; }

    public byte[] Vector { get; }

    public int TopK { get; }

    public FilterExpression? Filter { get; }

    public IReadOnlyList<string> LoadFields { get; }

    public IReadOnlyList<AggregationApply> ApplyClauses { get; }

    public AggregationGroupBy? GroupBy { get; }

    public AggregationSortBy? SortBy { get; }

    public int Offset { get; }

    public int Limit { get; }

    public string ScoreAlias { get; }

    public VectorKnnRuntimeOptions? RuntimeOptions { get; }

    internal FilterExpression CombinedFilter => Filter is null ? TextFilter : TextFilter & Filter;

    public static AggregateHybridQuery FromFloat32(
        FilterExpression textFilter,
        string vectorFieldName,
        float[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? loadFields = null,
        IEnumerable<AggregationApply>? applyClauses = null,
        AggregationGroupBy? groupBy = null,
        AggregationSortBy? sortBy = null,
        int offset = 0,
        int limit = 10,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null) =>
        new(
            textFilter,
            vectorFieldName,
            MemoryMarshal.AsBytes<float>(vector.AsSpan()).ToArray(),
            topK,
            filter,
            loadFields,
            applyClauses,
            groupBy,
            sortBy,
            offset,
            limit,
            scoreAlias,
            runtimeOptions);

    public static AggregateHybridQuery FromFloat64(
        FilterExpression textFilter,
        string vectorFieldName,
        double[] vector,
        int topK,
        FilterExpression? filter = null,
        IEnumerable<string>? loadFields = null,
        IEnumerable<AggregationApply>? applyClauses = null,
        AggregationGroupBy? groupBy = null,
        AggregationSortBy? sortBy = null,
        int offset = 0,
        int limit = 10,
        string scoreAlias = "vector_distance",
        VectorKnnRuntimeOptions? runtimeOptions = null) =>
        new(
            textFilter,
            vectorFieldName,
            MemoryMarshal.AsBytes<double>(vector.AsSpan()).ToArray(),
            topK,
            filter,
            loadFields,
            applyClauses,
            groupBy,
            sortBy,
            offset,
            limit,
            scoreAlias,
            runtimeOptions);

    private static IReadOnlyList<string> NormalizeFields(IEnumerable<string>? fields)
    {
        if (fields is null)
        {
            return [];
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field);
            var trimmed = field.Trim();
            var canonical = trimmed.TrimStart('@');
            if (seen.Add(canonical))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }
}
