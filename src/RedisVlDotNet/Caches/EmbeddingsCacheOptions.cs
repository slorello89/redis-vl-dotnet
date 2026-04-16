namespace RedisVl.Caches;

public sealed class EmbeddingsCacheOptions
{
    public EmbeddingsCacheOptions(string name, string? keyNamespace = null, TimeSpan? timeToLive = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (timeToLive.HasValue && timeToLive.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Cache TTL must be positive when provided.");
        }

        Name = name.Trim();
        KeyNamespace = string.IsNullOrWhiteSpace(keyNamespace) ? null : keyNamespace.Trim();
        TimeToLive = timeToLive;
    }

    public string Name { get; }

    public string? KeyNamespace { get; }

    public TimeSpan? TimeToLive { get; }
}
