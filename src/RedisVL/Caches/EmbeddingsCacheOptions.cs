namespace RedisVL.Caches;

/// <summary>Configuration for an <see cref="EmbeddingsCache" />, controlling its key naming and expiry.</summary>
public sealed class EmbeddingsCacheOptions
{
    /// <summary>Initializes a new <see cref="EmbeddingsCacheOptions" />.</summary>
    /// <param name="name">The cache name, used as part of every Redis key prefix.</param>
    /// <param name="keyNamespace">An optional namespace inserted into the key prefix to further partition entries.</param>
    /// <param name="timeToLive">An optional default expiry applied to stored entries; must be positive when provided.</param>
    /// <exception cref="ArgumentException"><paramref name="name" /> is <see langword="null" />, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeToLive" /> is zero or negative.</exception>
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

    /// <summary>Gets the cache name used as part of every Redis key prefix.</summary>
    public string Name { get; }

    /// <summary>Gets the optional namespace inserted into the key prefix, or <see langword="null" /> when unset.</summary>
    public string? KeyNamespace { get; }

    /// <summary>Gets the optional default expiry applied to stored entries, or <see langword="null" /> for no expiry.</summary>
    public TimeSpan? TimeToLive { get; }
}
