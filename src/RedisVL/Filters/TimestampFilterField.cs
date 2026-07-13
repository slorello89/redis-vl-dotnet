namespace RedisVL.Filters;

/// <summary>
/// Builds numeric range filters over a field that stores Unix epoch-seconds timestamps.
/// </summary>
public sealed class TimestampFilterField
{
    private readonly string _fieldName;

    internal TimestampFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    /// <summary>Matches timestamps strictly after the given instant.</summary>
    /// <param name="value">The exclusive lower bound.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression After(DateTimeOffset value) => After(value.ToUnixTimeSeconds());

    /// <summary>Matches timestamps strictly after the given Unix epoch-seconds value.</summary>
    /// <param name="epochSeconds">The exclusive lower bound, in seconds since the Unix epoch.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression After(long epochSeconds) =>
        new NumericFilterExpression(_fieldName, epochSeconds, double.PositiveInfinity, inclusiveMinimum: false, inclusiveMaximum: true);

    /// <summary>Matches timestamps strictly before the given instant.</summary>
    /// <param name="value">The exclusive upper bound.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression Before(DateTimeOffset value) => Before(value.ToUnixTimeSeconds());

    /// <summary>Matches timestamps strictly before the given Unix epoch-seconds value.</summary>
    /// <param name="epochSeconds">The exclusive upper bound, in seconds since the Unix epoch.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression Before(long epochSeconds) =>
        new NumericFilterExpression(_fieldName, double.NegativeInfinity, epochSeconds, inclusiveMinimum: true, inclusiveMaximum: false);

    /// <summary>Matches timestamps within an inclusive range between two instants.</summary>
    /// <param name="start">The inclusive start of the range.</param>
    /// <param name="end">The inclusive end of the range.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    public FilterExpression Between(DateTimeOffset start, DateTimeOffset end) =>
        Between(start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());

    /// <summary>Matches timestamps within an inclusive range between two Unix epoch-seconds values.</summary>
    /// <param name="startEpochSeconds">The inclusive start, in seconds since the Unix epoch.</param>
    /// <param name="endEpochSeconds">The inclusive end, in seconds since the Unix epoch.</param>
    /// <returns>A <see cref="FilterExpression"/> for the range.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="startEpochSeconds"/> is later than <paramref name="endEpochSeconds"/>.</exception>
    public FilterExpression Between(long startEpochSeconds, long endEpochSeconds)
    {
        if (startEpochSeconds > endEpochSeconds)
        {
            throw new ArgumentException("Timestamp filter start cannot be later than end.");
        }

        return new NumericFilterExpression(_fieldName, startEpochSeconds, endEpochSeconds, inclusiveMinimum: true, inclusiveMaximum: true);
    }

    /// <summary>Matches timestamps equal to the given instant.</summary>
    /// <param name="value">The instant to match.</param>
    /// <returns>A <see cref="FilterExpression"/> for the equality.</returns>
    public FilterExpression Eq(DateTimeOffset value) => Eq(value.ToUnixTimeSeconds());

    /// <summary>Matches timestamps equal to the given Unix epoch-seconds value.</summary>
    /// <param name="epochSeconds">The value to match, in seconds since the Unix epoch.</param>
    /// <returns>A <see cref="FilterExpression"/> for the equality.</returns>
    public FilterExpression Eq(long epochSeconds) =>
        new NumericFilterExpression(_fieldName, epochSeconds, epochSeconds, inclusiveMinimum: true, inclusiveMaximum: true);
}
