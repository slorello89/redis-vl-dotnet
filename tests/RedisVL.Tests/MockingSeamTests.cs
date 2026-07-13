using NSubstitute;
using RedisVL.Caches;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

namespace RedisVL.Tests;

/// <summary>
/// Demonstrates the mocking seam added for issue #51: consumers can depend on the core service
/// interfaces (<see cref="ISearchIndex" />, <see cref="ISemanticCache" />, …) and substitute a test
/// double with a standard mocking framework — no hand-rolled wrapper required.
/// </summary>
public sealed class MockingSeamTests
{
    // A sample consumer service that takes the index as an abstraction rather than the concrete type.
    private sealed class IndexHealthCheck(ISearchIndex index)
    {
        public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) =>
            index.ExistsAsync(cancellationToken);
    }

    // A sample consumer service that depends on the cache abstraction.
    private sealed class CachedResponder(ISemanticCache cache)
    {
        public async Task<string?> RespondAsync(string prompt, float[] embedding)
        {
            var hit = await cache.CheckAsync(prompt, embedding);
            return hit?.Response;
        }
    }

    [Fact]
    public async Task ConsumerDependingOnISearchIndex_CanBeUnitTestedWithAMock()
    {
        var index = Substitute.For<ISearchIndex>();
        index.ExistsAsync(Arg.Any<CancellationToken>()).Returns(true);

        var service = new IndexHealthCheck(index);

        Assert.True(await service.IsReadyAsync());
        await index.Received(1).ExistsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumerDependingOnISemanticCache_CanBeUnitTestedWithAMock()
    {
        var cache = Substitute.For<ISemanticCache>();
        cache
            .CheckAsync("what is redis?", Arg.Any<float[]>(), Arg.Any<FilterExpression?>(), Arg.Any<CancellationToken>())
            .Returns(new SemanticCacheHit("what is redis?", "an in-memory data store", 0.05));

        var service = new CachedResponder(cache);

        var response = await service.RespondAsync("what is redis?", [0.1f, 0.2f]);

        Assert.Equal("an in-memory data store", response);
    }

    [Fact]
    public async Task SemanticCache_UsesInjectedISearchIndex_ForItsSearches()
    {
        // The internal constructor lets the cache run against a substitute index, so the cache's own
        // logic can be exercised without a real Redis connection or a real index.
        var index = Substitute.For<ISearchIndex>();
        index
            .SearchAsync(Arg.Any<VectorRangeQuery>(), Arg.Any<CancellationToken>())
            .Returns(new SearchResults(0, Array.Empty<SearchDocument>()));

        var options = new SemanticCacheOptions(
            "seam-test",
            new VectorFieldAttributes(VectorAlgorithm.Flat, VectorDataType.Float32, VectorDistanceMetric.Cosine, 3),
            distanceThreshold: 0.2);
        var cache = new SemanticCache(Substitute.For<IDatabase>(), options, index);

        var hit = await cache.CheckAsync("prompt", [0.1f, 0.2f, 0.3f]);

        Assert.Null(hit);
        await index.Received(1).SearchAsync(Arg.Any<VectorRangeQuery>(), Arg.Any<CancellationToken>());
    }
}
