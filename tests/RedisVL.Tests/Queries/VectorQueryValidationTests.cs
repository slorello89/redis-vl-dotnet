using RedisVL.Queries;

namespace RedisVL.Tests.Queries;

public sealed class VectorQueryValidationTests
{
    private static byte[] SampleVector() => [1, 2, 3, 4];

    [Fact]
    public void VectorRangeQueryAllowsZeroDistanceThreshold()
    {
        // Zero is a legitimate radius for exact-duplicate detection (verified against a live server).
        var query = new VectorRangeQuery("embedding", SampleVector(), distanceThreshold: 0);

        Assert.Equal(0, query.DistanceThreshold);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public void VectorRangeQueryRejectsNegativeOrNaNDistanceThreshold(double distanceThreshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new VectorRangeQuery("embedding", SampleVector(), distanceThreshold));
    }

    [Fact]
    public void VectorQueryFactoriesThrowArgumentNullOnNullVector()
    {
        Assert.Throws<ArgumentNullException>(() => VectorQuery.FromFloat32("embedding", null!, 2));
        Assert.Throws<ArgumentNullException>(() => VectorQuery.FromFloat64("embedding", null!, 2));
    }

    [Fact]
    public void VectorRangeQueryFactoriesThrowArgumentNullOnNullVector()
    {
        Assert.Throws<ArgumentNullException>(() => VectorRangeQuery.FromFloat32("embedding", null!, 0.5));
        Assert.Throws<ArgumentNullException>(() => VectorRangeQuery.FromFloat64("embedding", null!, 0.5));
    }

    [Fact]
    public void VectorGetterReturnsDefensiveCopy()
    {
        var query = new VectorQuery("embedding", SampleVector(), 2);

        var first = query.Vector;
        first[0] = 0xFF;

        // Mutating the returned array must not affect the query's internal state.
        Assert.Equal(1, query.Vector[0]);
    }

    [Fact]
    public void VectorRangeQueryGetterReturnsDefensiveCopy()
    {
        var query = new VectorRangeQuery("embedding", SampleVector(), distanceThreshold: 0.5);

        var first = query.Vector;
        first[0] = 0xFF;

        Assert.Equal(1, query.Vector[0]);
    }
}
