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

    public FilterExpression After(DateTimeOffset value) => After(value.ToUnixTimeSeconds());

    public FilterExpression After(long epochSeconds) =>
        new NumericFilterExpression(_fieldName, epochSeconds, double.PositiveInfinity, inclusiveMinimum: false, inclusiveMaximum: true);

    public FilterExpression Before(DateTimeOffset value) => Before(value.ToUnixTimeSeconds());

    public FilterExpression Before(long epochSeconds) =>
        new NumericFilterExpression(_fieldName, double.NegativeInfinity, epochSeconds, inclusiveMinimum: true, inclusiveMaximum: false);

    public FilterExpression Between(DateTimeOffset start, DateTimeOffset end) =>
        Between(start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());

    public FilterExpression Between(long startEpochSeconds, long endEpochSeconds)
    {
        if (startEpochSeconds > endEpochSeconds)
        {
            throw new ArgumentException("Timestamp filter start cannot be later than end.");
        }

        return new NumericFilterExpression(_fieldName, startEpochSeconds, endEpochSeconds, inclusiveMinimum: true, inclusiveMaximum: true);
    }

    public FilterExpression Eq(DateTimeOffset value) => Eq(value.ToUnixTimeSeconds());

    public FilterExpression Eq(long epochSeconds) =>
        new NumericFilterExpression(_fieldName, epochSeconds, epochSeconds, inclusiveMinimum: true, inclusiveMaximum: true);
}
