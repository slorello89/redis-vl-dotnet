namespace RedisVl.Queries;

public sealed class AggregationGroupBy
{
    public AggregationGroupBy(
        IEnumerable<string>? properties = null,
        IEnumerable<AggregationReducer>? reducers = null)
    {
        Properties = NormalizeProperties(properties);
        Reducers = reducers?.ToArray() ?? [];

        if (Properties.Count == 0 && Reducers.Count == 0)
        {
            throw new ArgumentException("Aggregation group definitions must include at least one property or reducer.");
        }
    }

    public IReadOnlyList<string> Properties { get; }

    public IReadOnlyList<AggregationReducer> Reducers { get; }

    private static IReadOnlyList<string> NormalizeProperties(IEnumerable<string>? properties)
    {
        if (properties is null)
        {
            return [];
        }

        var normalized = new List<string>();
        foreach (var property in properties)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(property);
            normalized.Add(property.Trim());
        }

        return normalized;
    }
}
