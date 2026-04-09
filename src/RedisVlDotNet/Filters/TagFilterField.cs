namespace RedisVlDotNet.Filters;

public sealed class TagFilterField
{
    private readonly string _fieldName;

    internal TagFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    public FilterExpression Eq(string value) => In([value]);

    public FilterExpression In(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length == 0)
        {
            throw new ArgumentException("At least one tag value is required.", nameof(values));
        }

        return new TagFilterExpression(_fieldName, values);
    }
}
