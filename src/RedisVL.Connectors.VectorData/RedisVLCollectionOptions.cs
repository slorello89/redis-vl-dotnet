using Microsoft.Extensions.VectorData;

namespace RedisVL.Connectors.VectorData;

/// <summary>
/// Options controlling how a <see cref="RedisVLCollection{TKey, TRecord}"/> maps a record type
/// onto a RedisVL search index.
/// </summary>
public sealed class RedisVLCollectionOptions
{
    /// <summary>
    /// An explicit record definition. When omitted, the record type's
    /// <c>[VectorStoreKey]</c> / <c>[VectorStoreData]</c> / <c>[VectorStoreVector]</c> attributes are used.
    /// </summary>
    public VectorStoreCollectionDefinition? Definition { get; set; }

    /// <summary>
    /// The Redis key prefix for documents in this collection. Defaults to <c>"{collectionName}:"</c>.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// The name of the RediSearch index backing this collection. Defaults to the collection name.
    /// </summary>
    public string? IndexName { get; set; }
}
