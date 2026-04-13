namespace RedisVlDotNet.Queries;

public sealed class AggregationSortField
{
    public AggregationSortField(string property, bool descending = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        Property = property.Trim();
        Descending = descending;
    }

    public string Property { get; }

    public bool Descending { get; }
}
