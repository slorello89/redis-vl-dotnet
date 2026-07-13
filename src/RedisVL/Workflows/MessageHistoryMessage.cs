namespace RedisVL.Workflows;

/// <summary>A single stored chat message, as returned by <see cref="MessageHistory" /> and <see cref="SemanticMessageHistory" />.</summary>
public sealed class MessageHistoryMessage
{
    /// <summary>Initializes a new <see cref="MessageHistoryMessage" />.</summary>
    /// <param name="sessionId">The session the message belongs to.</param>
    /// <param name="role">The role of the message author (for example <c>user</c> or <c>assistant</c>).</param>
    /// <param name="content">The message content.</param>
    /// <param name="timestamp">The time the message was recorded.</param>
    /// <param name="metadata">Optional serialized metadata associated with the message.</param>
    /// <param name="sequence">The monotonic per-session sequence number; must be non-negative.</param>
    /// <param name="key">The Redis key the message is stored under, when known.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId" />, <paramref name="role" />, or <paramref name="content" /> is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sequence" /> is negative.</exception>
    public MessageHistoryMessage(
        string sessionId,
        string role,
        string content,
        DateTimeOffset timestamp,
        string? metadata = null,
        long sequence = 0,
        string? key = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "Message sequence cannot be negative.");
        }

        SessionId = sessionId.Trim();
        Role = role.Trim();
        Content = content.Trim();
        Timestamp = timestamp;
        Metadata = metadata;
        Sequence = sequence;
        Key = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    /// <summary>Gets the session the message belongs to.</summary>
    public string SessionId { get; }

    /// <summary>Gets the role of the message author.</summary>
    public string Role { get; }

    /// <summary>Gets the message content.</summary>
    public string Content { get; }

    /// <summary>Gets the time the message was recorded.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Gets the optional serialized metadata associated with the message.</summary>
    public string? Metadata { get; }

    /// <summary>Gets the monotonic per-session sequence number.</summary>
    public long Sequence { get; }

    /// <summary>Gets the Redis key the message is stored under, or <see langword="null" /> when not known.</summary>
    public string? Key { get; }
}
