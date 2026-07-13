using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A query that returns only the number of documents matching a filter expression, without retrieving any
/// document content.
/// </summary>
public sealed class CountQuery
{
    /// <summary>
    /// Initializes a new <see cref="CountQuery"/>.
    /// </summary>
    /// <param name="filter">The filter expression to count against; when <see langword="null"/> all documents are counted.</param>
    public CountQuery(FilterExpression? filter = null)
    {
        Filter = filter;
    }

    /// <summary>The filter expression applied to the index, or <see langword="null"/> to count all documents.</summary>
    public FilterExpression? Filter { get; }
}
