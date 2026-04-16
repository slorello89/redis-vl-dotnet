using RedisVL.Filters;

namespace RedisVL.Queries;

public sealed class CountQuery
{
    public CountQuery(FilterExpression? filter = null)
    {
        Filter = filter;
    }

    public FilterExpression? Filter { get; }
}
