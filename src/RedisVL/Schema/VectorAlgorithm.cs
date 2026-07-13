namespace RedisVL.Schema;

/// <summary>The indexing algorithm used for a vector field.</summary>
public enum VectorAlgorithm
{
    /// <summary>Brute-force exact search (<c>FLAT</c>).</summary>
    Flat = 0,

    /// <summary>Hierarchical Navigable Small World approximate search (<c>HNSW</c>).</summary>
    Hnsw = 1,

    /// <summary>Intel SVS-VAMANA graph-based approximate search (<c>SVS-VAMANA</c>).</summary>
    SvsVamana = 2
}
