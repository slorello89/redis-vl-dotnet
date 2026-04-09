using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

public sealed class FilterQuery
{
    public FilterQuery(
        FilterExpression? filter = null,
        IEnumerable<string>? returnFields = null,
        int offset = 0,
        int limit = 10)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit cannot be negative.");
        }

        Filter = filter;
        Offset = offset;
        Limit = limit;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
    }

    public FilterExpression? Filter { get; }

    public int Offset { get; }

    public int Limit { get; }

    public IReadOnlyList<string> ReturnFields { get; }
}
