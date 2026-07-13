namespace RedisVL.Queries;

/// <summary>
/// Represents a single argument passed to an <see cref="AggregationReducer"/>, which is either a
/// reference to a document property (rendered as <c>@name</c>) or a literal value.
/// </summary>
public sealed class AggregationReducerArgument
{
    private AggregationReducerArgument(string value, bool isPropertyReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
        IsPropertyReference = isPropertyReference;
    }

    /// <summary>
    /// Gets the argument's value: a property name when <see cref="IsPropertyReference"/> is
    /// <see langword="true"/>, otherwise a literal.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets a value indicating whether this argument references a document property rather than a literal.
    /// </summary>
    public bool IsPropertyReference { get; }

    /// <summary>
    /// Creates an argument that references the given document property.
    /// </summary>
    /// <param name="property">The property name to reference.</param>
    /// <returns>The property-reference argument.</returns>
    public static AggregationReducerArgument Property(string property) =>
        new(property, true);

    /// <summary>
    /// Creates an argument that carries a literal value.
    /// </summary>
    /// <param name="value">The literal value.</param>
    /// <returns>The literal argument.</returns>
    public static AggregationReducerArgument Literal(string value) =>
        new(value, false);
}
