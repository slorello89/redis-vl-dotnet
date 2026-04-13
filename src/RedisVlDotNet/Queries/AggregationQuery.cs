namespace RedisVlDotNet.Queries;

public sealed class AggregationQuery
{
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

    public string QueryString { get; }

    public IReadOnlyList<string> LoadFields { get; }

    public IReadOnlyList<AggregationApply> ApplyClauses { get; }

    public AggregationGroupBy? GroupBy { get; }

    public AggregationSortBy? SortBy { get; }

    public int Offset { get; }

    public int Limit { get; }

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
