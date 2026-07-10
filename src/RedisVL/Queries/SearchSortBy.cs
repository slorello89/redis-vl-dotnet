using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A single-field sort for an <c>FT.SEARCH</c> query (<c>SORTBY {field} [ASC|DESC]</c>).
/// <c>FT.SEARCH</c> accepts exactly one sort field; use an aggregation query to sort by multiple.
/// </summary>
public sealed class SearchSortBy
{
    public SearchSortBy(string field, bool descending = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        Field = FilterExpression.NormalizeFieldName(field);
        Descending = descending;
    }

    /// <summary>The index field (attribute) name to sort by, without a leading <c>@</c>.</summary>
    public string Field { get; }

    /// <summary>Whether to sort in descending order; ascending when <see langword="false"/>.</summary>
    public bool Descending { get; }
}
