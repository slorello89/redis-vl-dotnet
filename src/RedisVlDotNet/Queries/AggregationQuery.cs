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
        int limit = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryString);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit cannot be negative.");
        }

        QueryString = queryString.Trim();
        LoadFields = NormalizeFields(loadFields);
        ApplyClauses = applyClauses?.ToArray() ?? [];
        GroupBy = groupBy;
        SortBy = sortBy;
        Offset = offset;
        Limit = limit;
    }

    public string QueryString { get; }

    public IReadOnlyList<string> LoadFields { get; }

    public IReadOnlyList<AggregationApply> ApplyClauses { get; }

    public AggregationGroupBy? GroupBy { get; }

    public AggregationSortBy? SortBy { get; }

    public int Offset { get; }

    public int Limit { get; }

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
