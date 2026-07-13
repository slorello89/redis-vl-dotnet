using Microsoft.Extensions.VectorData;

namespace RedisVL.Connectors.VectorData.Mapping;

/// <summary>
/// Translates between the raw distance Redis reports for a KNN/range query and the score
/// semantics a Microsoft.Extensions.VectorData caller expects for the configured
/// <see cref="DistanceFunction"/>.
/// </summary>
/// <remarks>
/// Empirically, for a query vector <c>q</c> and stored vector <c>v</c> Redis returns:
/// <list type="bullet">
///   <item><description><c>COSINE</c>: <c>1 - cosineSimilarity(q, v)</c></description></item>
///   <item><description><c>L2</c>: the <em>squared</em> Euclidean distance <c>‖q - v‖²</c></description></item>
///   <item><description><c>IP</c>: <c>1 - dotProduct(q, v)</c></description></item>
/// </list>
/// MEVD, by contrast, defines the score in terms of the configured distance function: similarity
/// functions rank higher-is-better, distance functions rank lower-is-better.
/// </remarks>
internal static class VectorScoreTranslation
{
    /// <summary>Converts the raw Redis distance into the score for the property's distance function.</summary>
    public static double ToScore(RedisVLProperty vector, double redisDistance) =>
        vector.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity => 1.0 - redisDistance,
            DistanceFunction.DotProductSimilarity => 1.0 - redisDistance,
            DistanceFunction.NegativeDotProductSimilarity => redisDistance - 1.0,
            DistanceFunction.EuclideanDistance => Math.Sqrt(redisDistance),
            // CosineDistance (and the unspecified default, which indexes as cosine) report the raw
            // Redis distance unchanged.
            _ => redisDistance,
        };

    /// <summary>
    /// Converts a MEVD <c>ScoreThreshold</c> into the Redis distance radius for a
    /// <c>VECTOR_RANGE</c> query, so that keeping <c>distance ≤ radius</c> is equivalent to the
    /// threshold's filter direction. Throws when the threshold cannot be expressed as a positive
    /// radius (e.g. requiring cosine similarity ≥ 1, or a dot product above the [-1, 1] range that
    /// unnormalized vectors would need).
    /// </summary>
    public static double ToRangeRadius(RedisVLProperty vector, double threshold)
    {
        var radius = vector.DistanceFunction switch
        {
            DistanceFunction.CosineSimilarity => 1.0 - threshold,
            DistanceFunction.DotProductSimilarity => 1.0 - threshold,
            DistanceFunction.NegativeDotProductSimilarity => 1.0 + threshold,
            DistanceFunction.EuclideanDistance => threshold * threshold,
            _ => threshold,
        };

        if (radius <= 0)
        {
            throw new NotSupportedException(
                $"ScoreThreshold '{threshold}' cannot be expressed as a Redis range radius for distance function " +
                $"'{vector.DistanceFunction ?? "(default)"}'.");
        }

        return radius;
    }
}
