namespace RedisVL.Queries;

public sealed class AggregationReducerArgument
{
    private AggregationReducerArgument(string value, bool isPropertyReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
        IsPropertyReference = isPropertyReference;
    }

    public string Value { get; }

    public bool IsPropertyReference { get; }

    public static AggregationReducerArgument Property(string property) =>
        new(property, true);

    public static AggregationReducerArgument Literal(string value) =>
        new(value, false);
}
