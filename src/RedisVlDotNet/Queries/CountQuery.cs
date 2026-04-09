using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

public sealed class CountQuery
{
    public CountQuery(FilterExpression? filter = null)
    {
        Filter = filter;
    }

    public FilterExpression? Filter { get; }
}
