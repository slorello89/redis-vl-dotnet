using Microsoft.Extensions.VectorData;
using RedisVL.Connectors.VectorData;
using RedisVL.Tests.Indexes;
using StackExchange.Redis;

namespace RedisVL.Tests.Connectors.VectorData;

public sealed class RedisVLVectorStoreIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task UpsertGetSearchAndDelete_RoundTrips()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        var fetched = await collection.GetAsync("arrival");
        Assert.NotNull(fetched);
        Assert.Equal("Arrival", fetched!.Title);
        Assert.Equal(2016, fetched.Year);

        var query = new ReadOnlyMemory<float>([0.9f, 0.1f, 0.0f, 0.1f]);
        var matches = new List<VectorSearchResult<ConnectorMovie>>();
        await foreach (var result in collection.SearchAsync(query, top: 2))
        {
            matches.Add(result);
        }

        Assert.Equal(2, matches.Count);
        Assert.Equal("thematrix", matches[0].Record.Id);
        Assert.NotNull(matches[0].Score);

        await collection.DeleteAsync("arrival");
        Assert.Null(await collection.GetAsync("arrival"));
    }

    [RedisSearchIntegrationFact]
    public async Task SearchAsync_WithFilter_RestrictsResults()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        var query = new ReadOnlyMemory<float>([0.9f, 0.1f, 0.0f, 0.1f]);
        var genres = new HashSet<string>();
        await foreach (var result in collection.SearchAsync(
                           query,
                           top: 5,
                           new VectorSearchOptions<ConnectorMovie> { Filter = m => m.Genre == "crime" }))
        {
            genres.Add(result.Record.Genre);
        }

        Assert.Equal(["crime"], genres);
    }

    [RedisSearchIntegrationFact]
    public async Task GetAsync_WithFilter_ReturnsMatchingRecords()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        var titles = new List<string>();
        await foreach (var movie in collection.GetAsync(m => m.Genre == "crime" && m.Year == 1995, top: 10))
        {
            titles.Add(movie.Title);
        }

        Assert.Equal(2, titles.Count);
        Assert.Contains("Heat", titles);
        Assert.Contains("Se7en", titles);
    }

    [RedisSearchIntegrationFact]
    public async Task SearchAsync_WithOldFilter_ThrowsNotSupported()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var query = new ReadOnlyMemory<float>([0.9f, 0.1f, 0.0f, 0.1f]);
#pragma warning disable CS0618 // deliberately exercising rejection of the obsolete OldFilter option
        var options = new VectorSearchOptions<ConnectorMovie> { OldFilter = new VectorSearchFilter() };
#pragma warning restore CS0618

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in harness.Collection.SearchAsync(query, top: 5, options))
            {
            }
        });
    }

    [RedisSearchIntegrationFact]
    public async Task SearchAsync_WithScoreThreshold_FiltersByDistance()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        // The query vector is exactly The Matrix; the two sci-fi films sit at ~0 cosine distance
        // while the crime films are far (~0.78). ConnectorMovie uses CosineDistance, so the score
        // is the distance and the threshold is an upper bound on it.
        var query = new ReadOnlyMemory<float>([0.9f, 0.1f, 0.0f, 0.1f]);

        var near = new List<VectorSearchResult<ConnectorMovie>>();
        await foreach (var result in collection.SearchAsync(
                           query,
                           top: 10,
                           new VectorSearchOptions<ConnectorMovie> { ScoreThreshold = 0.1 }))
        {
            near.Add(result);
        }

        Assert.Equal(["scifi"], near.Select(r => r.Record.Genre).Distinct());
        Assert.All(near, r => Assert.True(r.Score <= 0.1 + 1e-6, $"score {r.Score} exceeded threshold"));

        // A threshold spanning the whole cosine-distance range keeps every document.
        var all = new List<VectorSearchResult<ConnectorMovie>>();
        await foreach (var result in collection.SearchAsync(
                           query,
                           top: 10,
                           new VectorSearchOptions<ConnectorMovie> { ScoreThreshold = 2.0 }))
        {
            all.Add(result);
        }

        Assert.Equal(4, all.Count);
        Assert.True(near.Count < all.Count, "the tighter threshold should return fewer results");
    }

    [RedisSearchIntegrationFact]
    public async Task GetAsync_WithSingleKeyOrderBy_SortsResults()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        var ascending = new List<int>();
        await foreach (var movie in collection.GetAsync(
                           m => m.Year > 0,
                           top: 10,
                           new FilteredRecordRetrievalOptions<ConnectorMovie> { OrderBy = o => o.Ascending(m => m.Year) }))
        {
            ascending.Add(movie.Year);
        }

        Assert.Equal([1995, 1995, 1999, 2016], ascending);

        var descending = new List<int>();
        await foreach (var movie in collection.GetAsync(
                           m => m.Year > 0,
                           top: 10,
                           new FilteredRecordRetrievalOptions<ConnectorMovie> { OrderBy = o => o.Descending(m => m.Year) }))
        {
            descending.Add(movie.Year);
        }

        Assert.Equal([2016, 1999, 1995, 1995], descending);
    }

    [RedisSearchIntegrationFact]
    public async Task GetAsync_WithMultiKeyOrderBy_ThrowsNotSupported()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var options = new FilteredRecordRetrievalOptions<ConnectorMovie>
        {
            OrderBy = o => o.Ascending(m => m.Genre).Descending(m => m.Year),
        };

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await foreach (var _ in harness.Collection.GetAsync(m => m.Genre == "scifi", top: 5, options))
            {
            }
        });
    }

    [RedisSearchIntegrationFact]
    public async Task SearchAsync_IncludeVectors_HonorsOption()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        var query = new ReadOnlyMemory<float>([0.9f, 0.1f, 0.0f, 0.1f]);

        // Default (IncludeVectors == false): the vector must not be materialized onto the record.
        var omitted = await FirstRecordAsync(collection.SearchAsync(query, top: 1));
        Assert.Equal(0, omitted.Embedding.Length);

        // IncludeVectors == true: the stored vector is returned.
        var included = await FirstRecordAsync(collection.SearchAsync(
            query,
            top: 1,
            new VectorSearchOptions<ConnectorMovie> { IncludeVectors = true }));
        Assert.Equal(4, included.Embedding.Length);
    }

    [RedisSearchIntegrationFact]
    public async Task GetAsync_ByKey_IncludeVectors_HonorsOption()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        var omitted = await collection.GetAsync("thematrix");
        Assert.NotNull(omitted);
        Assert.Equal(0, omitted!.Embedding.Length);

        var included = await collection.GetAsync(
            "thematrix",
            new RecordRetrievalOptions { IncludeVectors = true });
        Assert.NotNull(included);
        Assert.Equal(4, included!.Embedding.Length);
    }

    [RedisSearchIntegrationFact]
    public async Task CollectionExists_TracksLifecycle()
    {
        var name = $"vectordata-it-{Guid.NewGuid():N}";
        await using var connection = (RedisConnection)await RedisConnection.ConnectAsync();
        var store = new RedisVLVectorStore(connection.Database);

        Assert.False(await store.CollectionExistsAsync(name));

        var collection = store.GetCollection<string, ConnectorMovie>(name);
        await collection.EnsureCollectionExistsAsync();
        Assert.True(await store.CollectionExistsAsync(name));

        await store.EnsureCollectionDeletedAsync(name);
        Assert.False(await store.CollectionExistsAsync(name));
    }

    [RedisSearchIntegrationFact]
    public async Task GetAsync_LinqOperators_TranslateAndExecute()
    {
        await using var harness = await ConnectorTestHarness.CreateAsync($"vectordata-it-{Guid.NewGuid():N}");
        var collection = harness.Collection;

        await collection.UpsertAsync(SampleMovies());
        await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(harness.Index, 4);

        // numeric range
        Assert.Equal(["Arrival"], await TitlesAsync(collection, m => m.Year >= 2000));

        // OR
        Assert.Equal(
            ["Arrival", "Heat", "Se7en"],
            await TitlesAsync(collection, m => m.Year < 1996 || m.Year > 2015));

        // IN (Contains over a constant collection)
        var sciFiGenres = new[] { "scifi" };
        Assert.Equal(
            ["Arrival", "The Matrix"],
            await TitlesAsync(collection, m => sciFiGenres.Contains(m.Genre)));

        // negation
        Assert.Equal(
            ["Heat", "Se7en"],
            await TitlesAsync(collection, m => !(m.Genre == "scifi")));
    }

    private static async Task<ConnectorMovie> FirstRecordAsync(
        IAsyncEnumerable<VectorSearchResult<ConnectorMovie>> results)
    {
        await foreach (var result in results)
        {
            return result.Record;
        }

        throw new InvalidOperationException("Expected at least one search result.");
    }

    private static async Task<string[]> TitlesAsync(
        RedisVLCollection<string, ConnectorMovie> collection,
        System.Linq.Expressions.Expression<Func<ConnectorMovie, bool>> filter)
    {
        var titles = new List<string>();
        await foreach (var movie in collection.GetAsync(filter, top: 50))
        {
            titles.Add(movie.Title);
        }

        titles.Sort(StringComparer.Ordinal);
        return titles.ToArray();
    }

    private static ConnectorMovie[] SampleMovies() =>
    [
        new() { Id = "thematrix", Title = "The Matrix", Genre = "scifi", Year = 1999, Embedding = new[] { 0.9f, 0.1f, 0.0f, 0.1f } },
        new() { Id = "heat", Title = "Heat", Genre = "crime", Year = 1995, Embedding = new[] { 0.1f, 0.9f, 0.1f, 0.0f } },
        new() { Id = "arrival", Title = "Arrival", Genre = "scifi", Year = 2016, Embedding = new[] { 0.85f, 0.15f, 0.05f, 0.1f } },
        new() { Id = "se7en", Title = "Se7en", Genre = "crime", Year = 1995, Embedding = new[] { 0.05f, 0.85f, 0.2f, 0.0f } },
    ];

    private sealed class ConnectorTestHarness : IAsyncDisposable
    {
        private readonly RedisConnection _connection;

        private ConnectorTestHarness(RedisConnection connection, RedisVLCollection<string, ConnectorMovie> collection)
        {
            _connection = connection;
            Collection = collection;
            Index = (RedisVL.Indexes.SearchIndex)collection.GetService(typeof(RedisVL.Indexes.SearchIndex))!;
        }

        public RedisVLCollection<string, ConnectorMovie> Collection { get; }

        public RedisVL.Indexes.SearchIndex Index { get; }

        public static async Task<ConnectorTestHarness> CreateAsync(string name)
        {
            var connection = (RedisConnection)await RedisConnection.ConnectAsync();
            var collection = new RedisVLCollection<string, ConnectorMovie>(connection.Database, name);
            await collection.EnsureCollectionExistsAsync();
            return new ConnectorTestHarness(connection, collection);
        }

        public async ValueTask DisposeAsync()
        {
            await Collection.EnsureCollectionDeletedAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class RedisConnection : IAsyncDisposable
    {
        private readonly IConnectionMultiplexer _multiplexer;

        private RedisConnection(IConnectionMultiplexer multiplexer)
        {
            _multiplexer = multiplexer;
            Database = multiplexer.GetDatabase();
        }

        public IDatabase Database { get; }

        public static async Task<RedisConnection> ConnectAsync() =>
            new(await RedisSearchTestEnvironment.ConnectAsync());

        public async ValueTask DisposeAsync()
        {
            await _multiplexer.CloseAsync();
            _multiplexer.Dispose();
        }
    }
}
