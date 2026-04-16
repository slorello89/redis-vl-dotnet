namespace RedisVl.Filters;

public sealed class NumericFilterField
{
    private readonly string _fieldName;

    internal NumericFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    public FilterExpression Eq(double value) => Between(value, value);

    public FilterExpression Between(double minimum, double maximum, bool inclusiveMinimum = true, bool inclusiveMaximum = true)
    {
        if (minimum > maximum)
        {
            throw new ArgumentException("Numeric filter minimum cannot be greater than maximum.");
        }

        return new NumericFilterExpression(_fieldName, minimum, maximum, inclusiveMinimum, inclusiveMaximum);
    }

    public FilterExpression GreaterThan(double value) =>
        new NumericFilterExpression(_fieldName, value, double.PositiveInfinity, inclusiveMinimum: false, inclusiveMaximum: true);

    public FilterExpression GreaterThanOrEqualTo(double value) =>
        new NumericFilterExpression(_fieldName, value, double.PositiveInfinity, inclusiveMinimum: true, inclusiveMaximum: true);

    public FilterExpression LessThan(double value) =>
        new NumericFilterExpression(_fieldName, double.NegativeInfinity, value, inclusiveMinimum: true, inclusiveMaximum: false);

    public FilterExpression LessThanOrEqualTo(double value) =>
        new NumericFilterExpression(_fieldName, double.NegativeInfinity, value, inclusiveMinimum: true, inclusiveMaximum: true);
}
