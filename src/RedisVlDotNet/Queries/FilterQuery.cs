using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

public sealed class FilterQuery
{
    public FilterQuery(
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        int offset = 0,
        int limit = 10,
        QueryPagination? pagination = null)
    {
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Filter = filter;
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
    }

    public FilterExpression? Filter { get; }

    public int Offset { get; }

    public int Limit { get; }

    public QueryPagination Pagination { get; }

    public IReadOnlyList<string> ReturnFields { get; }
}
