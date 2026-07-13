namespace RedisVL.Queries;

/// <summary>
/// Represents a <c>SORTBY</c> stage of an <c>FT.AGGREGATE</c> pipeline, ordering results by one or
/// more <see cref="AggregationSortField"/> definitions.
/// </summary>
public sealed class AggregationSortBy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationSortBy"/> class.
    /// </summary>
    /// <param name="fields">The sort fields, applied in order; at least one is required.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fields"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fields"/> is empty.</exception>
    public AggregationSortBy(IEnumerable<AggregationSortField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Fields = fields.ToArray();
        if (Fields.Count == 0)
        {
            throw new ArgumentException("Aggregation sort definitions must include at least one field.", nameof(fields));
        }
    }

    /// <summary>
    /// Gets the sort fields, applied in order.
    /// </summary>
    public IReadOnlyList<AggregationSortField> Fields { get; }
}
