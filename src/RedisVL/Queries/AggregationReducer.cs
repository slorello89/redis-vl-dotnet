using System.Globalization;

namespace RedisVL.Queries;

public sealed class AggregationReducer
{
    public AggregationReducer(
        string functionName,
        IEnumerable<AggregationReducerArgument>? arguments,
        string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        FunctionName = functionName.Trim().ToUpperInvariant();
        Alias = alias.TrimStart('@').Trim();
        Arguments = arguments?.ToArray() ?? [];
    }

    public string FunctionName { get; }

    public string Alias { get; }

    public IReadOnlyList<AggregationReducerArgument> Arguments { get; }

    public static AggregationReducer Count(string alias) =>
        new("COUNT", [], alias);

    public static AggregationReducer Sum(string property, string alias) =>
        new("SUM", [AggregationReducerArgument.Property(property)], alias);

    public static AggregationReducer Average(string property, string alias) =>
        new("AVG", [AggregationReducerArgument.Property(property)], alias);

    public static AggregationReducer Min(string property, string alias) =>
        new("MIN", [AggregationReducerArgument.Property(property)], alias);

    public static AggregationReducer Max(string property, string alias) =>
        new("MAX", [AggregationReducerArgument.Property(property)], alias);

    public static AggregationReducer CountDistinct(string property, string alias) =>
        new("COUNT_DISTINCT", [AggregationReducerArgument.Property(property)], alias);

    public static AggregationReducer CountDistinctish(string property, string alias) =>
        new("COUNT_DISTINCTISH", [AggregationReducerArgument.Property(property)], alias);

    public static AggregationReducer ToList(string property, string alias) =>
        new("TOLIST", [AggregationReducerArgument.Property(property)], alias);

    public static AggregationReducer Quantile(string property, double percentile, string alias)
    {
        if (percentile is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), percentile, "Percentile must be between 0 and 1.");
        }

        return new(
            "QUANTILE",
            [
                AggregationReducerArgument.Property(property),
                AggregationReducerArgument.Literal(percentile.ToString("G", CultureInfo.InvariantCulture))
            ],
            alias);
    }
}
