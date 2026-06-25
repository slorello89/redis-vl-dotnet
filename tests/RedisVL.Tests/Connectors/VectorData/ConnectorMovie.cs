using Microsoft.Extensions.VectorData;

namespace RedisVL.Tests.Connectors.VectorData;

/// <summary>Shared record fixture for vector-data connector tests.</summary>
public sealed class ConnectorMovie
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public string Genre { get; set; } = string.Empty;

    [VectorStoreData(IsIndexed = true)]
    public int Year { get; set; }

    [VectorStoreData]
    public string Summary { get; set; } = string.Empty;

    [VectorStoreVector(4, DistanceFunction = DistanceFunction.CosineDistance, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
