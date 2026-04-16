using RedisVL.Schema;

namespace RedisVL.Workflows;

public sealed class SemanticMessageHistoryOptions
{
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

    public string Name { get; }

    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    public double DistanceThreshold { get; }

    public string? KeyNamespace { get; }

    public string SessionIdFieldName { get; }

    public string RoleFieldName { get; }

    public string ContentFieldName { get; }

    public string MetadataFieldName { get; }

    public string TimestampFieldName { get; }

    public string SequenceFieldName { get; }

    public string EmbeddingFieldName { get; }
}
