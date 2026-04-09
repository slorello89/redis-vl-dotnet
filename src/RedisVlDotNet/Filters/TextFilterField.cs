namespace RedisVlDotNet.Filters;

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
}
