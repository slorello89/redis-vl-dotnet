namespace RedisVlDotNet.Queries;

public sealed class TextQuery
{
    public TextQuery(
        string text,
        IEnumerable<string>? returnFields = null,
        int offset = 0,
        int limit = 10,
        QueryPagination? pagination = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Text = text.Trim();
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
    }

    public string Text { get; }

    public int Offset { get; }

    public int Limit { get; }

    public QueryPagination Pagination { get; }

    public IReadOnlyList<string> ReturnFields { get; }
}
