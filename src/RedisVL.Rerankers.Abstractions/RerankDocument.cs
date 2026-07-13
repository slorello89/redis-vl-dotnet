namespace RedisVL.Rerankers;

/// <summary>
/// A candidate document to be reranked, with optional identifier and caller-supplied metadata.
/// </summary>
public sealed class RerankDocument
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RerankDocument"/> class.
    /// </summary>
    /// <param name="text">The document text scored by the reranker.</param>
    /// <param name="id">An optional caller-defined identifier for the document.</param>
    /// <param name="metadata">Optional caller-supplied metadata carried alongside the document.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <c>null</c>.</exception>
    public RerankDocument(string text, string? id = null, object? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        Id = id;
        Metadata = metadata;
    }

    /// <summary>Gets the document text scored by the reranker.</summary>
    public string Text { get; }

    /// <summary>Gets the optional caller-defined identifier for the document.</summary>
    public string? Id { get; }

    /// <summary>Gets the optional caller-supplied metadata carried alongside the document.</summary>
    public object? Metadata { get; }
}
