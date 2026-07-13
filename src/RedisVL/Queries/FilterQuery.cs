using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A non-scored <c>FT.SEARCH</c> query that returns documents matching a filter expression, with optional
/// sorting and pagination but no text or vector ranking.
/// </summary>
public sealed class FilterQuery
{
    /// <summary>
    /// Initializes a new <see cref="FilterQuery"/>.
    /// </summary>
    /// <param name="filter">The filter expression; when <see langword="null"/> all documents match.</param>
    /// <param name="returnFields">The fields to return for each match; when <see langword="null"/> all fields are returned.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="pagination">Optional pagination window; overrides <paramref name="offset"/> and <paramref name="limit"/> when supplied.</param>
    /// <param name="sortBy">An optional single-field sort.</param>
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

    /// <summary>The filter expression applied to the index, or <see langword="null"/> to match all documents.</summary>
    public FilterExpression? Filter { get; }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }

    /// <summary>The pagination window applied to the results.</summary>
    public QueryPagination Pagination { get; }

    /// <summary>The fields returned for each matching document.</summary>
    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>Optional single-field sort (<c>SORTBY</c>); results are unordered when <see langword="null"/>.</summary>
    public SearchSortBy? SortBy { get; }
}
