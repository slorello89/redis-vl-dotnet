namespace RedisVL.Schema;

/// <summary>The Redis data structure used to store the documents an index tracks (<c>ON</c>).</summary>
public enum StorageType
{
    /// <summary>Documents are stored as Redis hashes (<c>ON HASH</c>).</summary>
    Hash = 0,

    /// <summary>Documents are stored as RedisJSON documents (<c>ON JSON</c>).</summary>
    Json = 1
}
