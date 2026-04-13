namespace RedisVlDotNet.Workflows;

public sealed class MessageHistoryOptions
{
    public MessageHistoryOptions(
        string name,
        string? keyNamespace = null,
        string sessionIdFieldName = "sessionId",
        string roleFieldName = "role",
        string contentFieldName = "content",
        string metadataFieldName = "metadata",
        string timestampFieldName = "timestamp",
        string sequenceFieldName = "sequence")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionIdFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timestampFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sequenceFieldName);

        Name = name.Trim();
        KeyNamespace = string.IsNullOrWhiteSpace(keyNamespace) ? null : keyNamespace.Trim();
        SessionIdFieldName = sessionIdFieldName.Trim();
        RoleFieldName = roleFieldName.Trim();
        ContentFieldName = contentFieldName.Trim();
        MetadataFieldName = metadataFieldName.Trim();
        TimestampFieldName = timestampFieldName.Trim();
        SequenceFieldName = sequenceFieldName.Trim();
    }

    public string Name { get; }

    public string? KeyNamespace { get; }

    public string SessionIdFieldName { get; }

    public string RoleFieldName { get; }

    public string ContentFieldName { get; }

    public string MetadataFieldName { get; }

    public string TimestampFieldName { get; }

    public string SequenceFieldName { get; }
}
