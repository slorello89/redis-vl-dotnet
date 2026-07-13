using System.Globalization;

namespace RedisVL.Queries;

/// <summary>
/// Represents a <c>REDUCE</c> function applied within a <c>GROUPBY</c> stage of an <c>FT.AGGREGATE</c>
/// pipeline (for example <c>COUNT</c>, <c>SUM</c>, or <c>AVG</c>), together with its arguments and
/// output alias.
/// </summary>
public sealed class AggregationReducer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AggregationReducer"/> class.
    /// </summary>
    /// <param name="functionName">The reducer function name (for example <c>COUNT</c> or <c>SUM</c>); normalized to upper case.</param>
    /// <param name="arguments">The arguments passed to the reducer, or <see langword="null"/> for none.</param>
    /// <param name="alias">The alias under which the reducer's result is emitted (a leading <c>@</c> is stripped).</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="functionName"/> or <paramref name="alias"/> is null or whitespace.</exception>
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

    /// <summary>
    /// Gets the reducer function name (for example <c>COUNT</c> or <c>SUM</c>), in upper case.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// Gets the alias under which the reducer's result is emitted.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the arguments passed to the reducer function.
    /// </summary>
    public IReadOnlyList<AggregationReducerArgument> Arguments { get; }

    /// <summary>
    /// Creates a <c>COUNT</c> reducer that counts the records in each group.
    /// </summary>
    /// <param name="alias">The alias under which the count is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer Count(string alias) =>
        new("COUNT", [], alias);

    /// <summary>
    /// Creates a <c>SUM</c> reducer that sums the given property across each group.
    /// </summary>
    /// <param name="property">The property to sum.</param>
    /// <param name="alias">The alias under which the sum is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer Sum(string property, string alias) =>
        new("SUM", [AggregationReducerArgument.Property(property)], alias);

    /// <summary>
    /// Creates an <c>AVG</c> reducer that averages the given property across each group.
    /// </summary>
    /// <param name="property">The property to average.</param>
    /// <param name="alias">The alias under which the average is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer Average(string property, string alias) =>
        new("AVG", [AggregationReducerArgument.Property(property)], alias);

    /// <summary>
    /// Creates a <c>MIN</c> reducer that returns the minimum value of the given property in each group.
    /// </summary>
    /// <param name="property">The property to take the minimum of.</param>
    /// <param name="alias">The alias under which the minimum is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer Min(string property, string alias) =>
        new("MIN", [AggregationReducerArgument.Property(property)], alias);

    /// <summary>
    /// Creates a <c>MAX</c> reducer that returns the maximum value of the given property in each group.
    /// </summary>
    /// <param name="property">The property to take the maximum of.</param>
    /// <param name="alias">The alias under which the maximum is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer Max(string property, string alias) =>
        new("MAX", [AggregationReducerArgument.Property(property)], alias);

    /// <summary>
    /// Creates a <c>COUNT_DISTINCT</c> reducer that counts the distinct values of the given property in each group.
    /// </summary>
    /// <param name="property">The property whose distinct values are counted.</param>
    /// <param name="alias">The alias under which the count is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer CountDistinct(string property, string alias) =>
        new("COUNT_DISTINCT", [AggregationReducerArgument.Property(property)], alias);

    /// <summary>
    /// Creates a <c>COUNT_DISTINCTISH</c> reducer that returns an approximate count of the distinct
    /// values of the given property in each group.
    /// </summary>
    /// <param name="property">The property whose distinct values are approximately counted.</param>
    /// <param name="alias">The alias under which the count is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer CountDistinctish(string property, string alias) =>
        new("COUNT_DISTINCTISH", [AggregationReducerArgument.Property(property)], alias);

    /// <summary>
    /// Creates a <c>TOLIST</c> reducer that collects the distinct values of the given property in each group into a list.
    /// </summary>
    /// <param name="property">The property whose values are collected.</param>
    /// <param name="alias">The alias under which the list is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    public static AggregationReducer ToList(string property, string alias) =>
        new("TOLIST", [AggregationReducerArgument.Property(property)], alias);

    /// <summary>
    /// Creates a <c>QUANTILE</c> reducer that returns the value of the given property at the specified percentile within each group.
    /// </summary>
    /// <param name="property">The property to compute the quantile of.</param>
    /// <param name="percentile">The percentile to compute, between <c>0</c> and <c>1</c> inclusive.</param>
    /// <param name="alias">The alias under which the quantile value is emitted.</param>
    /// <returns>The configured <see cref="AggregationReducer"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="percentile"/> is outside the range <c>0</c> to <c>1</c>.</exception>
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
