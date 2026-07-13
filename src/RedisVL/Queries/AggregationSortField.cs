namespace RedisVL.Queries;

/// <summary>
/// Describes a single property and sort direction within an <c>FT.AGGREGATE</c> <c>SORTBY</c> stage.
/// </summary>
public sealed class AggregationSortField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationSortField"/> class.
    /// </summary>
    /// <param name="property">The property to sort by.</param>
    /// <param name="descending"><see langword="true"/> to sort descending (<c>DESC</c>); otherwise ascending (<c>ASC</c>).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="property"/> is null or whitespace.</exception>
    public AggregationSortField(string property, bool descending = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        Property = property.Trim();
        Descending = descending;
    }

    /// <summary>
    /// Gets the property to sort by.
    /// </summary>
    public string Property { get; }

    /// <summary>
    /// Gets a value indicating whether the sort is descending (<c>DESC</c>) rather than ascending (<c>ASC</c>).
    /// </summary>
    public bool Descending { get; }
}
