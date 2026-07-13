namespace RedisVL.Workflows;

/// <summary>Configuration for a <see cref="MessageHistory" />, including index naming and the hash field names used for each message attribute.</summary>
public sealed class MessageHistoryOptions
{
    /// <summary>Initializes a new <see cref="MessageHistoryOptions" />.</summary>
    /// <param name="name">The history name, used to derive the index name and key prefixes.</param>
    /// <param name="keyNamespace">An optional namespace that isolates this history's keys and index from others sharing a name.</param>
    /// <param name="sessionIdFieldName">The hash field and index field storing the session id.</param>
    /// <param name="roleFieldName">The hash field and index field storing the message role.</param>
    /// <param name="contentFieldName">The hash field and index field storing the message content.</param>
    /// <param name="metadataFieldName">The hash field and index field storing serialized metadata.</param>
    /// <param name="timestampFieldName">The hash field and index field storing the Unix-millisecond timestamp.</param>
    /// <param name="sequenceFieldName">The hash field and index field storing the monotonic sequence number.</param>
    /// <exception cref="ArgumentException">A required name argument is null or whitespace.</exception>
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

    /// <summary>Gets the history name, used to derive the index name and key prefixes.</summary>
    public string Name { get; }

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
}
