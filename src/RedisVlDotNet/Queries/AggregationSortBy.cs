namespace RedisVl.Queries;

public sealed class AggregationSortBy
{
    public AggregationSortBy(IEnumerable<AggregationSortField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Fields = fields.ToArray();
        if (Fields.Count == 0)
        {
            throw new ArgumentException("Aggregation sort definitions must include at least one field.", nameof(fields));
        }
    }

    public IReadOnlyList<AggregationSortField> Fields { get; }
}
