namespace RedisVL.Schema;

/// <summary>
/// Vector compression algorithms supported by the SVS-VAMANA vector index.
/// </summary>
public enum VectorCompression
{
    /// <summary>No compression (default).</summary>
    None = 0,

    /// <summary>Scalar LVQ with 8-bit quantization.</summary>
    Lvq8 = 1,

    /// <summary>Scalar LVQ with 4-bit quantization.</summary>
    Lvq4 = 2,

    /// <summary>Two-level LVQ with 4-bit primary and 4-bit residual quantization.</summary>
    Lvq4x4 = 3,

    /// <summary>Two-level LVQ with 4-bit primary and 8-bit residual quantization.</summary>
    Lvq4x8 = 4,

    /// <summary>LeanVec dimensionality reduction with 4-bit primary and 8-bit secondary quantization.</summary>
    LeanVec4x8 = 5,

    /// <summary>LeanVec dimensionality reduction with 8-bit primary and 8-bit secondary quantization.</summary>
    LeanVec8x8 = 6
}
