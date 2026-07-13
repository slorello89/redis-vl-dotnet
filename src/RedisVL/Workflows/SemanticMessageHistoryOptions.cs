using RedisVL.Schema;

namespace RedisVL.Workflows;

/// <summary>Configuration for a <see cref="SemanticMessageHistory" />, including index naming, field names, and embedding attributes.</summary>
public sealed class SemanticMessageHistoryOptions
{
    /// <summary>Initializes a new <see cref="SemanticMessageHistoryOptions" />.</summary>
    /// <param name="name">The history name, used to derive the index name and key prefixes.</param>
    /// <param name="embeddingFieldAttributes">The vector field attributes describing the embedding; must be <see cref="VectorDataType.Float32" />.</param>
    /// <param name="distanceThreshold">The default maximum distance for relevance queries; must be greater than zero.</param>
    /// <param name="keyNamespace">An optional namespace that isolates this history's keys and index from others sharing a name.</param>
    /// <param name="sessionIdFieldName">The hash field and index field storing the session id.</param>
    /// <param name="roleFieldName">The hash field and index field storing the message role.</param>
    /// <param name="contentFieldName">The hash field and index field storing the message content.</param>
    /// <param name="metadataFieldName">The hash field and index field storing serialized metadata.</param>
    /// <param name="timestampFieldName">The hash field and index field storing the Unix-millisecond timestamp.</param>
    /// <param name="sequenceFieldName">The hash field and index field storing the monotonic sequence number.</param>
    /// <param name="embeddingFieldName">The hash field and index field storing the content embedding.</param>
    /// <exception cref="ArgumentException">A required name argument is null or whitespace, or the embedding is not FLOAT32.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="distanceThreshold" /> is not greater than zero.</exception>
    public SemanticMessageHistoryOptions(
        string name,
        VectorFieldAttributes embeddingFieldAttributes,
        double distanceThreshold,
        string? keyNamespace = null,
        string sessionIdFieldName = "sessionId",
        string roleFieldName = "role",
        string contentFieldName = "content",
        string metadataFieldName = "metadata",
        string timestampFieldName = "timestamp",
        string sequenceFieldName = "sequence",
        string embeddingFieldName = "embedding")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(embeddingFieldAttributes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionIdFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timestampFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingFieldName);

        if (distanceThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), distanceThreshold, "Semantic message history distance threshold must be greater than zero.");
        }

        if (embeddingFieldAttributes.DataType != VectorDataType.Float32)
        {
            throw new ArgumentException("Semantic message history currently supports only FLOAT32 embeddings.", nameof(embeddingFieldAttributes));
        }

        Name = name.Trim();
        EmbeddingFieldAttributes = embeddingFieldAttributes;
        DistanceThreshold = distanceThreshold;
        KeyNamespace = string.IsNullOrWhiteSpace(keyNamespace) ? null : keyNamespace.Trim();
        SessionIdFieldName = sessionIdFieldName.Trim();
        RoleFieldName = roleFieldName.Trim();
        ContentFieldName = contentFieldName.Trim();
        MetadataFieldName = metadataFieldName.Trim();
        TimestampFieldName = timestampFieldName.Trim();
        SequenceFieldName = sequenceFieldName.Trim();
        EmbeddingFieldName = embeddingFieldName.Trim();
    }

    /// <summary>Gets the history name, used to derive the index name and key prefixes.</summary>
    public string Name { get; }

    /// <summary>Gets the vector field attributes describing the stored content embeddings.</summary>
    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    /// <summary>Gets the default maximum distance a message must be within to be considered relevant.</summary>
    public double DistanceThreshold { get; }

    /// <summary>Gets the optional namespace that isolates this history's keys and index from others sharing a name.</summary>
    public string? KeyNamespace { get; }

    /// <summary>Gets the field name storing the session id.</summary>
    public string SessionIdFieldName { get; }

    /// <summary>Gets the field name storing the message role.</summary>
    public string RoleFieldName { get; }

    /// <summary>Gets the field name storing the message content.</summary>
    public string ContentFieldName { get; }

    /// <summary>Gets the field name storing serialized message metadata.</summary>
    public string MetadataFieldName { get; }

    /// <summary>Gets the field name storing the message timestamp as Unix-time milliseconds.</summary>
    public string TimestampFieldName { get; }

    /// <summary>Gets the field name storing the monotonic per-session sequence number.</summary>
    public string SequenceFieldName { get; }

    /// <summary>Gets the field name storing the content embedding.</summary>
    public string EmbeddingFieldName { get; }
}
