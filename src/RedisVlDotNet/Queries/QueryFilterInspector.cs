using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

internal static class QueryFilterInspector
{
    public static bool ContainsTextExpression(FilterExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        return expression switch
        {
            TextFilterExpression => true,
            LogicalFilterExpression logical => logical.Expressions.Any(ContainsTextExpression),
            NotFilterExpression negated => ContainsTextExpression(negated.Expression),
            _ => false
        };
    }
}
