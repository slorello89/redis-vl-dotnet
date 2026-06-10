namespace RedisVL.Filters;

public sealed class TextFilterField
{
    private readonly string _fieldName;

    internal TextFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    public FilterExpression Match(string term) =>
        new TextFilterExpression(_fieldName, FilterExpression.EscapeTextTerm(term));

    public FilterExpression Phrase(string phrase) =>
        new TextFilterExpression(_fieldName, $"\"{FilterExpression.EscapePhrase(phrase)}\"");

    public FilterExpression Prefix(string prefix) =>
        new TextFilterExpression(_fieldName, $"{FilterExpression.EscapeTextTerm(prefix)}*");

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

    public FilterExpression Wildcard(string pattern) =>
        new TextFilterExpression(_fieldName, $"w'{FilterExpression.EscapeWildcardPattern(pattern)}'");
}
