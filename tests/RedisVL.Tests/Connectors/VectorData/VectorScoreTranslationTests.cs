using Microsoft.Extensions.VectorData;
using RedisVL.Connectors.VectorData.Mapping;

namespace RedisVL.Tests.Connectors.VectorData;

public sealed class VectorScoreTranslationTests
{
    private static RedisVLProperty Vector(string? distanceFunction) =>
        new(typeof(ConnectorMovie).GetProperty(nameof(ConnectorMovie.Embedding))!, "embedding", RedisVLFieldKind.Vector)
        {
            DistanceFunction = distanceFunction,
        };

    // Raw Redis distance d -> reported score, per configured distance function.
    [Theory]
    [InlineData(DistanceFunction.CosineDistance, 0.25, 0.25)]
    [InlineData(DistanceFunction.CosineSimilarity, 0.25, 0.75)]
    [InlineData(DistanceFunction.DotProductSimilarity, 0.25, 0.75)]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, 0.25, -0.75)]
    [InlineData(DistanceFunction.EuclideanDistance, 0.25, 0.5)]
    [InlineData(null, 0.25, 0.25)]
    public void ToScore_ConvertsRawDistance(string? distanceFunction, double redisDistance, double expected)
    {
        Assert.Equal(expected, VectorScoreTranslation.ToScore(Vector(distanceFunction), redisDistance), 6);
    }

    // MEVD ScoreThreshold -> Redis VECTOR_RANGE radius (a distance).
    [Theory]
    [InlineData(DistanceFunction.CosineDistance, 0.2, 0.2)]
    [InlineData(DistanceFunction.CosineSimilarity, 0.8, 0.2)]
    [InlineData(DistanceFunction.DotProductSimilarity, 0.8, 0.2)]
    [InlineData(DistanceFunction.NegativeDotProductSimilarity, -0.8, 0.2)]
    [InlineData(DistanceFunction.EuclideanDistance, 0.5, 0.25)]
    [InlineData(null, 0.2, 0.2)]
    public void ToRangeRadius_ConvertsThreshold(string? distanceFunction, double threshold, double expected)
    {
        Assert.Equal(expected, VectorScoreTranslation.ToRangeRadius(Vector(distanceFunction), threshold), 6);
    }

    [Theory]
    [InlineData(DistanceFunction.CosineSimilarity, 1.0)]   // radius 0
    [InlineData(DistanceFunction.DotProductSimilarity, 1.5)] // radius -0.5
    [InlineData(DistanceFunction.CosineDistance, 0.0)]      // radius 0
    public void ToRangeRadius_ThrowsWhenThresholdInexpressible(string distanceFunction, double threshold)
    {
        Assert.Throws<NotSupportedException>(() => VectorScoreTranslation.ToRangeRadius(Vector(distanceFunction), threshold));
    }
}
