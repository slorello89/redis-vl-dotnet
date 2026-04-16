namespace RedisVl.Queries;

public sealed class AggregationApply
{
    public AggregationApply(string expression, string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expression);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        Expression = expression.Trim();
        Alias = alias.TrimStart('@').Trim();
    }

    public string Expression { get; }

    public string Alias { get; }
}
