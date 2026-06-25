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
