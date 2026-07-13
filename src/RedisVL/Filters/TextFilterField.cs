namespace RedisVL.Filters;

/// <summary>
/// Builds full-text query filters over a <c>TEXT</c> field, including term, phrase, prefix,
/// fuzzy, and wildcard matching.
/// </summary>
public sealed class TextFilterField
{
    private readonly string _fieldName;

    internal TextFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    /// <summary>Matches documents containing the given (tokenized) term.</summary>
    /// <param name="term">The term to match; special characters are escaped.</param>
    /// <returns>A <see cref="FilterExpression"/> for the term match.</returns>
    public FilterExpression Match(string term) =>
        new TextFilterExpression(_fieldName, FilterExpression.EscapeTextTerm(term));

    /// <summary>Matches documents containing the exact ordered phrase.</summary>
    /// <param name="phrase">The phrase to match; quotes and backslashes are escaped.</param>
    /// <returns>A <see cref="FilterExpression"/> for the phrase match.</returns>
    public FilterExpression Phrase(string phrase) =>
        new TextFilterExpression(_fieldName, $"\"{FilterExpression.EscapePhrase(phrase)}\"");

    /// <summary>Matches documents whose terms start with the given prefix.</summary>
    /// <param name="prefix">The prefix to match; a trailing <c>*</c> wildcard is appended.</param>
    /// <returns>A <see cref="FilterExpression"/> for the prefix match.</returns>
    public FilterExpression Prefix(string prefix) =>
        new TextFilterExpression(_fieldName, $"{FilterExpression.EscapeTextTerm(prefix)}*");

    /// <summary>Matches documents containing a term within a Levenshtein edit distance of the given term.</summary>
    /// <param name="term">The term to fuzzy-match.</param>
    /// <param name="maxEditDistance">The maximum edit distance, between 1 and 3.</param>
    /// <returns>A <see cref="FilterExpression"/> for the fuzzy match.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxEditDistance"/> is not between 1 and 3.</exception>
    public FilterExpression Fuzzy(string term, int maxEditDistance = 1)
    {
        if (maxEditDistance is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEditDistance),
                maxEditDistance,
                "Fuzzy match edit distance must be between 1 and 3.");
        }

        var delimiter = new string('%', maxEditDistance);
        return new TextFilterExpression(_fieldName, $"{delimiter}{FilterExpression.EscapeTextTerm(term)}{delimiter}");
    }

    /// <summary>Matches documents using a wildcard pattern (<c>w'...'</c>) where <c>*</c> and <c>?</c> act as wildcards.</summary>
    /// <param name="pattern">The wildcard pattern; the delimiter quote and backslash are escaped.</param>
    /// <returns>A <see cref="FilterExpression"/> for the wildcard match.</returns>
    public FilterExpression Wildcard(string pattern) =>
        new TextFilterExpression(_fieldName, $"w'{FilterExpression.EscapeWildcardPattern(pattern)}'");
}
