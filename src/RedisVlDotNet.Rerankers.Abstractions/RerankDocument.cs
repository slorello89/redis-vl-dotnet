namespace RedisVl.Rerankers;

public sealed class RerankDocument
{
    public RerankDocument(string text, string? id = null, object? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        Id = id;
        Metadata = metadata;
    }

    public string Text { get; }

    public string? Id { get; }

    public object? Metadata { get; }
}
