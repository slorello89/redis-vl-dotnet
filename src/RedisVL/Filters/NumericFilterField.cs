namespace RedisVL.Filters;

/// <summary>
/// Builds numeric range and equality filters over a <c>NUMERIC</c> field.
/// </summary>
public sealed class NumericFilterField
{
    private readonly string _fieldName;

    internal NumericFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    /// <summary>Matches values equal to <paramref name="value"/>.</summary>
    /// <param name="value">The value to match.</param>
    /// <returns>A <see cref="FilterExpression"/> for the equality.</returns>
    public FilterExpression Eq(double value) => Between(value, value);

    /// <summary>Matches values within a range, with configurable inclusivity at each bound.</summary>
    /// <param name="minimum">The lower bound.</param>
    /// <param name="maximum">The upper bound.</param>
    /// <param name="inclusiveMinimum">Whether the lower bound is inclusive.</param>
    /// <param name="inclusiveMaximum">Whether the upper bound is inclusive.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="minimum"/> or <paramref name="maximum"/> is <see cref="double.NaN"/>, or when <paramref name="minimum"/> is greater than <paramref name="maximum"/>.</exception>
    public FilterExpression Between(double minimum, double maximum, bool inclusiveMinimum = true, bool inclusiveMaximum = true)
    {
        // NaN comparisons are always false, so a NaN bound would slip past the min > max check and
        // only fail at the server as a query syntax error; reject it up front with a clear message.
        if (double.IsNaN(minimum) || double.IsNaN(maximum))
        {
            throw new ArgumentException("Numeric filter bounds cannot be NaN.");
        }

        if (minimum > maximum)
        {
            throw new ArgumentException("Numeric filter minimum cannot be greater than maximum.");
        }

        return new NumericFilterExpression(_fieldName, minimum, maximum, inclusiveMinimum, inclusiveMaximum);
    }

    /// <summary>Matches values strictly greater than <paramref name="value"/>.</summary>
    /// <param name="value">The exclusive lower bound.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression GreaterThan(double value) =>
        new NumericFilterExpression(_fieldName, value, double.PositiveInfinity, inclusiveMinimum: false, inclusiveMaximum: true);

    /// <summary>Matches values greater than or equal to <paramref name="value"/>.</summary>
    /// <param name="value">The inclusive lower bound.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression GreaterThanOrEqualTo(double value) =>
        new NumericFilterExpression(_fieldName, value, double.PositiveInfinity, inclusiveMinimum: true, inclusiveMaximum: true);

    /// <summary>Matches values strictly less than <paramref name="value"/>.</summary>
    /// <param name="value">The exclusive upper bound.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression LessThan(double value) =>
        new NumericFilterExpression(_fieldName, double.NegativeInfinity, value, inclusiveMinimum: true, inclusiveMaximum: false);

    /// <summary>Matches values less than or equal to <paramref name="value"/>.</summary>
    /// <param name="value">The inclusive upper bound.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression LessThanOrEqualTo(double value) =>
        new NumericFilterExpression(_fieldName, double.NegativeInfinity, value, inclusiveMinimum: true, inclusiveMaximum: true);
}
