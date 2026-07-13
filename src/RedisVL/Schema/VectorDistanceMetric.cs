namespace RedisVL.Schema;

/// <summary>The distance metric used to compare vectors in a vector field (<c>DISTANCE_METRIC</c>).</summary>
public enum VectorDistanceMetric
{
    /// <summary>Cosine distance (<c>COSINE</c>).</summary>
    Cosine = 0,

    /// <summary>Euclidean (L2) distance (<c>L2</c>).</summary>
    L2 = 1,

    /// <summary>Negative inner (dot) product distance (<c>IP</c>).</summary>
    InnerProduct = 2
}
