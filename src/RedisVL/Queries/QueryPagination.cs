namespace RedisVL.Queries;

/// <summary>
/// A pagination window for a search query, expressed as an offset into the result set and a maximum number
/// of results to return (the <c>LIMIT</c> clause).
/// </summary>
public sealed class QueryPagination
{
    /// <summary>
    /// Initializes a new <see cref="QueryPagination"/>.
    /// </summary>
    /// <param name="offset">The number of leading results to skip; cannot be negative.</param>
    /// <param name="limit">The maximum number of results to return; cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> or <paramref name="limit"/> is negative.</exception>
    public QueryPagination(int offset = 0, int limit = 10)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit cannot be negative.");
        }

        Offset = offset;
        Limit = limit;
    }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }
}
