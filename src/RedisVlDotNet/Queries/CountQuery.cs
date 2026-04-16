using RedisVl.Filters;

namespace RedisVl.Queries;

public sealed class CountQuery
{
    public CountQuery(FilterExpression? filter = null)
    {
        Filter = filter;
    }

    public FilterExpression? Filter { get; }
}
