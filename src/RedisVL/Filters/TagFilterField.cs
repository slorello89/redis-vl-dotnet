namespace RedisVL.Filters;

/// <summary>
/// Builds exact-match and wildcard filters over a <c>TAG</c> field.
/// </summary>
public sealed class TagFilterField
{
    private readonly string _fieldName;

    internal TagFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    /// <summary>Matches documents whose tag set contains the exact value.</summary>
    /// <param name="value">The tag value to match.</param>
    /// <returns>A <see cref="FilterExpression"/> for the match.</returns>
    public FilterExpression Eq(string value) => In([value]);

    /// <summary>Matches documents whose tag set contains any of the given exact values.</summary>
    /// <param name="values">The tag values to match; at least one is required.</param>
    /// <returns>A <see cref="FilterExpression"/> for the match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no values are supplied.</exception>
    public FilterExpression In(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length == 0)
        {
            throw new ArgumentException("At least one tag value is required.", nameof(values));
        }

        return new TagFilterExpression(_fieldName, values);
    }

    /// <summary>Matches documents whose tag set matches any of the given wildcard patterns (preserving <c>*</c> and <c>?</c>).</summary>
    /// <param name="patterns">The wildcard patterns to match; at least one is required.</param>
    /// <returns>A <see cref="FilterExpression"/> for the match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="patterns"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when no patterns are supplied.</exception>
    public FilterExpression Like(params string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);

        if (patterns.Length == 0)
        {
            throw new ArgumentException("At least one tag pattern is required.", nameof(patterns));
        }

        return new TagFilterExpression(_fieldName, patterns, preserveWildcards: true);
    }
}
