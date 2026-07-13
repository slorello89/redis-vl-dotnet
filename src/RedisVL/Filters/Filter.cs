namespace RedisVL.Filters;

/// <summary>
/// Entry point for building query filter expressions. Provides factory methods that start a
/// filter for a named field and combinators for composing expressions with boolean logic.
/// </summary>
public static class Filter
{
    /// <summary>Starts a filter over a <c>TAG</c> field.</summary>
    /// <param name="fieldName">The field name, with or without a leading <c>@</c>.</param>
    /// <returns>A <see cref="TagFilterField"/> for building tag filters.</returns>
    public static TagFilterField Tag(string fieldName) => new(fieldName);

    /// <summary>Starts a filter over a <c>NUMERIC</c> field.</summary>
    /// <param name="fieldName">The field name, with or without a leading <c>@</c>.</param>
    /// <returns>A <see cref="NumericFilterField"/> for building numeric filters.</returns>
    public static NumericFilterField Numeric(string fieldName) => new(fieldName);

    /// <summary>Starts a filter over a <c>TEXT</c> field.</summary>
    /// <param name="fieldName">The field name, with or without a leading <c>@</c>.</param>
    /// <returns>A <see cref="TextFilterField"/> for building full-text filters.</returns>
    public static TextFilterField Text(string fieldName) => new(fieldName);

    /// <summary>Starts a filter over a <c>GEO</c> field.</summary>
    /// <param name="fieldName">The field name, with or without a leading <c>@</c>.</param>
    /// <returns>A <see cref="GeoFilterField"/> for building geospatial filters.</returns>
    public static GeoFilterField Geo(string fieldName) => new(fieldName);

    /// <summary>Starts a filter over a numeric field storing Unix epoch-seconds timestamps.</summary>
    /// <param name="fieldName">The field name, with or without a leading <c>@</c>.</param>
    /// <returns>A <see cref="TimestampFilterField"/> for building timestamp filters.</returns>
    public static TimestampFilterField Timestamp(string fieldName) => new(fieldName);

    /// <summary>Combines expressions with logical AND (intersection).</summary>
    /// <param name="expressions">The expressions to intersect; at least two are required.</param>
    /// <returns>A composed <see cref="FilterExpression"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when fewer than two expressions are supplied.</exception>
    public static FilterExpression And(params FilterExpression[] expressions) =>
        Combine(LogicalOperator.And, expressions);

    /// <summary>Combines expressions with logical OR (union).</summary>
    /// <param name="expressions">The expressions to union; at least two are required.</param>
    /// <returns>A composed <see cref="FilterExpression"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when fewer than two expressions are supplied.</exception>
    public static FilterExpression Or(params FilterExpression[] expressions) =>
        Combine(LogicalOperator.Or, expressions);

    /// <summary>Negates an expression with logical NOT.</summary>
    /// <param name="expression">The expression to negate.</param>
    /// <returns>A <see cref="FilterExpression"/> matching documents that do not match <paramref name="expression"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="expression"/> is <see langword="null"/>.</exception>
    public static FilterExpression Not(FilterExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return new NotFilterExpression(expression);
    }

    private static FilterExpression Combine(LogicalOperator operation, IReadOnlyCollection<FilterExpression> expressions)
    {
        ArgumentNullException.ThrowIfNull(expressions);

        if (expressions.Count < 2)
        {
            throw new ArgumentException("Logical composition requires at least two filter expressions.", nameof(expressions));
        }

        var flattened = new List<FilterExpression>();
        foreach (var expression in expressions)
        {
            ArgumentNullException.ThrowIfNull(expression);

            if (expression is LogicalFilterExpression logical && logical.Operation == operation)
            {
                flattened.AddRange(logical.Expressions);
                continue;
            }

            flattened.Add(expression);
        }

        return new LogicalFilterExpression(operation, flattened);
    }
}
