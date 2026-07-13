namespace RedisVL.Queries;

/// <summary>
/// Describes an <c>FT.AGGREGATE</c> pipeline: the filter query plus its optional <c>LOAD</c>,
/// <c>APPLY</c>, <c>GROUPBY</c>, <c>SORTBY</c>, and <c>LIMIT</c> stages.
/// </summary>
public sealed class AggregationQuery
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationQuery"/> class.
    /// </summary>
    /// <param name="queryString">The search filter expression; defaults to <c>*</c> (match all).</param>
    /// <param name="loadFields">The fields to <c>LOAD</c> from the source documents, or <see langword="null"/> for none.</param>
    /// <param name="applyClauses">The <c>APPLY</c> expressions to evaluate, or <see langword="null"/> for none.</param>
    /// <param name="groupBy">The optional <c>GROUPBY</c> stage.</param>
    /// <param name="sortBy">The optional <c>SORTBY</c> stage.</param>
    /// <param name="offset">The number of results to skip when no <paramref name="pagination"/> is supplied.</param>
    /// <param name="limit">The maximum number of results to return when no <paramref name="pagination"/> is supplied.</param>
    /// <param name="pagination">The pagination to apply; when <see langword="null"/> one is built from <paramref name="offset"/> and <paramref name="limit"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="queryString"/> is null or whitespace.</exception>
    public AggregationQuery(
        string queryString = "*",
        IEnumerable<string>? loadFields = null,
        IEnumerable<AggregationApply>? applyClauses = null,
        AggregationGroupBy? groupBy = null,
        AggregationSortBy? sortBy = null,
        int offset = 0,
        int limit = 10,
        QueryPagination? pagination = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryString);

        QueryString = queryString.Trim();
        LoadFields = NormalizeFields(loadFields);
        ApplyClauses = applyClauses?.ToArray() ?? [];
        GroupBy = groupBy;
        SortBy = sortBy;
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
    }

    /// <summary>
    /// Gets the search filter expression that selects the records fed into the pipeline.
    /// </summary>
    public string QueryString { get; }

    /// <summary>
    /// Gets the fields to <c>LOAD</c> from the source documents.
    /// </summary>
    public IReadOnlyList<string> LoadFields { get; }

    /// <summary>
    /// Gets the <c>APPLY</c> expressions evaluated in the pipeline.
    /// </summary>
    public IReadOnlyList<AggregationApply> ApplyClauses { get; }

    /// <summary>
    /// Gets the optional <c>GROUPBY</c> stage, or <see langword="null"/> if the pipeline does not group.
    /// </summary>
    public AggregationGroupBy? GroupBy { get; }

    /// <summary>
    /// Gets the optional <c>SORTBY</c> stage, or <see langword="null"/> if the pipeline does not sort.
    /// </summary>
    public AggregationSortBy? SortBy { get; }

    /// <summary>
    /// Gets the number of results to skip (the <c>LIMIT</c> offset).
    /// </summary>
    public int Offset { get; }

    /// <summary>
    /// Gets the maximum number of results to return (the <c>LIMIT</c> count).
    /// </summary>
    public int Limit { get; }

    /// <summary>
    /// Gets the pagination applied to the pipeline's output.
    /// </summary>
    public QueryPagination Pagination { get; }

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
