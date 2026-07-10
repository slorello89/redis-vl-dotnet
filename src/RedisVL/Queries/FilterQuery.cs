using RedisVL.Filters;

namespace RedisVL.Queries;

public sealed class FilterQuery
{
    public FilterQuery(
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        int offset = 0,
        int limit = 10,
        QueryPagination? pagination = null,
        SearchSortBy? sortBy = null)
    {
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Filter = filter;
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
        SortBy = sortBy;
    }

    public FilterExpression? Filter { get; }

    public int Offset { get; }

    public int Limit { get; }

    public QueryPagination Pagination { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>Optional single-field sort (<c>SORTBY</c>); results are unordered when <see langword="null"/>.</summary>
    public SearchSortBy? SortBy { get; }
}
