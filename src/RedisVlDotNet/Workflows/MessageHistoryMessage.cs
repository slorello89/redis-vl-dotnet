namespace RedisVl.Workflows;

public sealed class MessageHistoryMessage
{
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

    public string SessionId { get; }

    public string Role { get; }

    public string Content { get; }

    public DateTimeOffset Timestamp { get; }

    public string? Metadata { get; }

    public long Sequence { get; }

    public string? Key { get; }
}
