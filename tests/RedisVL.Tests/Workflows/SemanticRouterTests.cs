using System.Reflection;
using RedisVL.Caches;
using RedisVL.Schema;
using RedisVL.Vectorizers;
using RedisVL.Workflows;
using StackExchange.Redis;

namespace RedisVL.Tests.Workflows;

public sealed class SemanticRouterTests
{
    [Fact]
    public async Task RouteAsync_WithEmbeddingGenerator_UsesGeneratedEmbeddingAndReturnsNearestRoute()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncHandler = (command, _) => command switch
        {
            "FT.SEARCH" => Task.FromResult(
                RedisResult.Create(
                    [
                        RedisResult.Create(1),
                        RedisResult.Create((RedisValue)"semantic-router:unit-router:tests:key"),
                        RedisResult.Create(
                            [
                                RedisResult.Create((RedisValue)"routeName"),
                                RedisResult.Create((RedisValue)"billing"),
                                RedisResult.Create((RedisValue)"reference"),
                                RedisResult.Create((RedisValue)"refund status"),
                                RedisResult.Create((RedisValue)"distance"),
                                RedisResult.Create((RedisValue)"0.08")
                            ])
                    ])),
            _ => Task.FromResult(RedisResult.Create((RedisValue)"OK"))
        };

        var generator = new RecordingEmbeddingGenerator([1f, 0f]);
        var router = new SemanticRouter(database, CreateOptions());

        var match = await router.RouteAsync("where is my refund?", generator);

        Assert.NotNull(match);
        Assert.Equal("where is my refund?", generator.LastInput);
        Assert.Equal("where is my refund?", match!.Input);
        Assert.Equal("billing", match.RouteName);
        Assert.Equal("refund status", match.Reference);
        Assert.Equal(0.08d, match.Distance, 3);
    }

    [Fact]
    public async Task AddRouteAsync_WithEmbeddingGenerator_WritesHashDocument()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var generator = new RecordingEmbeddingGenerator([1f, 2f]);
        var router = new SemanticRouter(database, CreateOptions());

        var key = await router.AddRouteAsync("billing", "refund status", generator);

        Assert.Equal("refund status", generator.LastInput);
        Assert.Equal("semantic-router:unit-router:tests:376a9d27c9e5b12ced4415e8f2ae29947c7ebd7e6d9aa970c0455d53b434dc6a", key);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "routeName" && entry.Value == "billing");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "reference" && entry.Value == "refund status");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "embedding");
    }

    [Fact]
    public async Task RouteAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var router = new SemanticRouter(database, CreateOptions());

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            router.RouteAsync("hello", [1f, 0f], cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task RouteManyAsync_AveragesReferenceDistancesAndOrdersNearestFirst()
    {
        var (database, _) = CreateRouterWithSearchReply(
            ("billing", "0.1"), ("billing", "0.15"), ("support", "0.05"));
        var router = new SemanticRouter(database, CreateOptions());

        var matches = await router.RouteManyAsync("question", [1f, 0f], maxResults: 2);

        Assert.Equal(2, matches.Count);
        Assert.Equal("support", matches[0].RouteName);
        Assert.Equal(0.05d, matches[0].Distance, 5);
        Assert.Equal("billing", matches[1].RouteName);
        Assert.Equal(0.125d, matches[1].Distance, 5);
    }

    [Fact]
    public async Task RouteManyAsync_WithMinimumAggregation_UsesNearestReferencePerRoute()
    {
        var (database, _) = CreateRouterWithSearchReply(
            ("billing", "0.1"), ("billing", "0.15"), ("support", "0.05"));
        var router = new SemanticRouter(database, CreateOptions());

        var matches = await router.RouteManyAsync("question", [1f, 0f], maxResults: 2, aggregationMethod: DistanceAggregationMethod.Minimum);

        Assert.Equal(["support", "billing"], matches.Select(match => match.RouteName));
        Assert.Equal(0.1d, matches[1].Distance, 5);
    }

    [Fact]
    public async Task RouteManyAsync_WithSumAggregation_AddsReferenceDistancesPerRoute()
    {
        var (database, _) = CreateRouterWithSearchReply(
            ("billing", "0.1"), ("billing", "0.15"), ("support", "0.05"));
        var router = new SemanticRouter(database, CreateOptions());

        var matches = await router.RouteManyAsync("question", [1f, 0f], maxResults: 2, aggregationMethod: DistanceAggregationMethod.Sum);

        Assert.Equal(["support", "billing"], matches.Select(match => match.RouteName));
        Assert.Equal(0.25d, matches[1].Distance, 5);
    }

    [Fact]
    public async Task RouteManyAsync_RespectsMaxResults()
    {
        var (database, _) = CreateRouterWithSearchReply(
            ("billing", "0.1"), ("support", "0.05"));
        var router = new SemanticRouter(database, CreateOptions());

        var matches = await router.RouteManyAsync("question", [1f, 0f], maxResults: 1);

        Assert.Single(matches);
        Assert.Equal("support", matches[0].RouteName);
    }

    [Fact]
    public async Task RouteManyAsync_DropsRoutesOutsidePerRouteThreshold()
    {
        var (database, _) = CreateRouterWithSearchReplyEntries(
            new[]
            {
                ("k1", new[] { ("routeName", "billing"), ("distance", "0.1"), ("routeThreshold", "0.1") }),
                ("k2", new[] { ("routeName", "billing"), ("distance", "0.15"), ("routeThreshold", "0.1") }),
                ("k3", new[] { ("routeName", "support"), ("distance", "0.05") }),
            });
        var router = new SemanticRouter(database, CreateOptions());

        // billing average (0.125) exceeds its per-route threshold (0.1) and is dropped.
        var matches = await router.RouteManyAsync("question", [1f, 0f], maxResults: 5);

        Assert.Single(matches);
        Assert.Equal("support", matches[0].RouteName);
    }

    [Fact]
    public async Task AddRouteAsync_WithRoute_PersistsThresholdAndMetadataPerReference()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var router = new SemanticRouter(database, CreateOptions());

        var route = new Route(
            "billing",
            ["refund status", "chargeback"],
            new Dictionary<string, object?> { ["team"] = "finance" },
            distanceThreshold: 0.2d);

        var keys = await router.AddRouteAsync(route, new[] { new[] { 1f, 0f }, new[] { 0f, 1f } });

        Assert.Equal(2, keys.Count);
        Assert.Equal(2, recorder.HashSetCalls.Count);
        foreach (var entries in recorder.HashSetCalls)
        {
            Assert.Contains(entries, entry => entry.Name == "routeName" && entry.Value == "billing");
            Assert.Contains(entries, entry => entry.Name == "routeThreshold" && entry.Value == "0.2");
            Assert.Contains(entries, entry => entry.Name == "metadata" && entry.Value.ToString()!.Contains("finance"));
        }
    }

    [Fact]
    public async Task AddRouteReferencesAsync_WritesOneDocumentPerReference()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var router = new SemanticRouter(database, CreateOptions());

        var keys = await router.AddRouteReferencesAsync(
            "billing",
            ["refund status", "chargeback"],
            new[] { new[] { 1f, 0f }, new[] { 0f, 1f } });

        Assert.Equal(2, keys.Count);
        Assert.Equal(2, recorder.HashSetCalls.Count);
        Assert.All(recorder.HashSetCalls, entries =>
            Assert.Contains(entries, entry => entry.Name == "routeName" && entry.Value == "billing"));
    }

    [Fact]
    public async Task GetRouteReferencesAsync_MapsStoredReferences()
    {
        var (database, _) = CreateRouterWithSearchReplyEntries(
            new[]
            {
                ("k1", new[] { ("routeName", "billing"), ("reference", "refund status") }),
                ("k2", new[] { ("routeName", "billing"), ("reference", "chargeback") }),
            });
        var router = new SemanticRouter(database, CreateOptions());

        var references = await router.GetRouteReferencesAsync("billing");

        Assert.Equal(2, references.Count);
        Assert.Equal("k1", references[0].Key);
        Assert.Equal("refund status", references[0].Reference);
        Assert.All(references, reference => Assert.Equal("billing", reference.RouteName));
    }

    [Fact]
    public async Task DeleteRouteReferencesAsync_DeletesComputedKeys()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var router = new SemanticRouter(database, CreateOptions());

        var deleted = await router.DeleteRouteReferencesAsync("billing", ["refund status", "chargeback"]);

        Assert.Equal(2, deleted);
        Assert.Equal(2, recorder.DeletedKeys.Count);
        Assert.All(recorder.DeletedKeys, key => Assert.StartsWith("semantic-router:unit-router:tests:", key.ToString()));
    }

    [Fact]
    public async Task DeleteRouteAsync_QueriesThenDeletesEveryReference()
    {
        var (database, recorder) = CreateRouterWithSearchReplyEntries(
            new[]
            {
                ("semantic-router:unit-router:tests:k1", new[] { ("routeName", "billing") }),
                ("semantic-router:unit-router:tests:k2", new[] { ("routeName", "billing") }),
            });
        var router = new SemanticRouter(database, CreateOptions());

        var deleted = await router.DeleteRouteAsync("billing");

        Assert.Equal(2, deleted);
        Assert.Equal(
            ["semantic-router:unit-router:tests:k1", "semantic-router:unit-router:tests:k2"],
            recorder.DeletedKeys.Select(key => key.ToString()));
    }

    private static (IDatabase Database, RecordingDatabaseProxy Recorder) CreateRouterWithSearchReply(
        params (string RouteName, string Distance)[] documents)
    {
        var entries = documents
            .Select((document, index) =>
                ($"k{index}", new[] { ("routeName", document.RouteName), ("distance", document.Distance) }))
            .ToArray();
        return CreateRouterWithSearchReplyEntries(entries);
    }

    private static (IDatabase Database, RecordingDatabaseProxy Recorder) CreateRouterWithSearchReplyEntries(
        (string Key, (string Field, string Value)[] Fields)[] documents)
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncHandler = (command, _) => command switch
        {
            "FT.SEARCH" => Task.FromResult(BuildSearchReply(documents)),
            _ => Task.FromResult(RedisResult.Create((RedisValue)"OK"))
        };
        return (database, recorder);
    }

    private static RedisResult BuildSearchReply((string Key, (string Field, string Value)[] Fields)[] documents)
    {
        var items = new List<RedisResult> { RedisResult.Create(documents.Length) };
        foreach (var (key, fields) in documents)
        {
            items.Add(RedisResult.Create((RedisValue)key));
            var fieldItems = new List<RedisResult>();
            foreach (var (field, value) in fields)
            {
                fieldItems.Add(RedisResult.Create((RedisValue)field));
                fieldItems.Add(RedisResult.Create((RedisValue)value));
            }

            items.Add(RedisResult.Create(fieldItems.ToArray()));
        }

        return RedisResult.Create(items.ToArray());
    }

    private static SemanticRouterOptions CreateOptions() =>
        new("unit-router", CreateVectorAttributes(), 0.3d, "tests");

    private static VectorFieldAttributes CreateVectorAttributes() =>
        new(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.L2,
            2);

    private sealed class RecordingEmbeddingGenerator(float[] embedding) : ITextVectorizer
    {
        public string? LastInput { get; private set; }

        public Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(embedding);
        }
    }

    private class RecordingDatabaseProxy : DispatchProxy
    {
        public Func<string, object?[]?, Task<RedisResult>>? ExecuteAsyncHandler { get; set; }

        public int ExecuteAsyncCallCount { get; private set; }

        public int HashSetAsyncCallCount { get; private set; }

        public HashEntry[]? LastHashEntries { get; private set; }

        public List<HashEntry[]> HashSetCalls { get; } = [];

        public List<RedisKey> DeletedKeys { get; } = [];

        public static (IDatabase Database, RecordingDatabaseProxy Recorder) CreatePair()
        {
            var database = DispatchProxy.Create<IDatabase, RecordingDatabaseProxy>();
            var recorder = (RecordingDatabaseProxy)(object)database;
            return (database, recorder);
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            return targetMethod.Name switch
            {
                nameof(IDatabase.ExecuteAsync) => HandleExecuteAsync(args),
                nameof(IDatabase.HashSetAsync) => HandleHashSetAsync(args),
                nameof(IDatabase.KeyDeleteAsync) => HandleKeyDeleteAsync(args),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
        }

        private Task<RedisResult> HandleExecuteAsync(object?[]? args)
        {
            ExecuteAsyncCallCount++;
            var command = (string)args![0]!;
            return ExecuteAsyncHandler is not null
                ? ExecuteAsyncHandler(command, args)
                : Task.FromResult(RedisResult.Create((RedisValue)"OK"));
        }

        private Task<bool> HandleHashSetAsync(object?[]? args)
        {
            HashSetAsyncCallCount++;
            LastHashEntries = (HashEntry[])args![1]!;
            HashSetCalls.Add(LastHashEntries);
            return Task.FromResult(true);
        }

        private Task<long> HandleKeyDeleteAsync(object?[]? args)
        {
            if (args![0] is RedisKey[] keys)
            {
                DeletedKeys.AddRange(keys);
                return Task.FromResult((long)keys.Length);
            }

            DeletedKeys.Add((RedisKey)args[0]!);
            return Task.FromResult(1L);
        }
    }
}
