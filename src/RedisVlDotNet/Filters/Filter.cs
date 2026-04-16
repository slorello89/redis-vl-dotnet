namespace RedisVl.Filters;

public static class Filter
{
    public static TagFilterField Tag(string fieldName) => new(fieldName);

    public static NumericFilterField Numeric(string fieldName) => new(fieldName);

    public static TextFilterField Text(string fieldName) => new(fieldName);

    public static GeoFilterField Geo(string fieldName) => new(fieldName);

    public static FilterExpression And(params FilterExpression[] expressions) =>
        Combine(LogicalOperator.And, expressions);

    public static FilterExpression Or(params FilterExpression[] expressions) =>
        Combine(LogicalOperator.Or, expressions);

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
