namespace RedisVlDotNet.Queries;

public sealed class TextQuery
{
    public TextQuery(
        string text,
        IEnumerable<string>? returnFields = null,
        int offset = 0,
        int limit = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit cannot be negative.");
        }

        Text = text.Trim();
        Offset = offset;
        Limit = limit;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
    }

    public string Text { get; }

    public int Offset { get; }

    public int Limit { get; }

    public IReadOnlyList<string> ReturnFields { get; }
}
