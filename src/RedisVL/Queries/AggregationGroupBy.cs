namespace RedisVL.Queries;

/// <summary>
/// Represents a <c>GROUPBY</c> stage of an <c>FT.AGGREGATE</c> pipeline: the properties to group by
/// and the <see cref="AggregationReducer"/> functions applied to each group.
/// </summary>
public sealed class AggregationGroupBy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationGroupBy"/> class.
    /// </summary>
    /// <param name="properties">The properties to group by, or <see langword="null"/> for none.</param>
    /// <param name="reducers">The reducers applied to each group, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentException">Thrown when neither any property nor any reducer is supplied.</exception>
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

    /// <summary>
    /// Gets the properties the pipeline groups by.
    /// </summary>
    public IReadOnlyList<string> Properties { get; }

    /// <summary>
    /// Gets the reducers applied to each group.
    /// </summary>
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
