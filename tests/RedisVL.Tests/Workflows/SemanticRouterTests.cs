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
    public async Task AddRouteAsync_WithEmbeddingLengthNotMatchingDimensions_ThrowsAndDoesNotWrite()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var router = new SemanticRouter(database, CreateOptions());

        // The schema declares two dimensions; a three-value embedding would be silently rejected by
        // RediSearch on write, so the add must fail loudly before dispatching any command.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            router.AddRouteAsync("billing", "refund status", [1f, 2f, 3f]));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
        Assert.Empty(recorder.HashSetCalls);
    }

    [Fact]
    public async Task AddRouteAsync_SingleReference_WritesReferenceWithoutTouchingRouteConfig()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var router = new SemanticRouter(database, CreateOptions());

        await router.AddRouteAsync("billing", "refund status", [1f, 0f]);

        // A bare reference add stores only reference fields via a plain HSET. Route-level config lives in a
        // separate key, so a single add must not open a transaction, delete fields, or clear the route's
        // threshold/metadata.
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Equal(0, recorder.CreateTransactionCallCount);
        Assert.Equal(0, recorder.HashDeleteAsyncCallCount);
        Assert.Empty(recorder.DeletedKeys);
        Assert.DoesNotContain(recorder.LastHashEntries!, entry => entry.Name == "routeThreshold");
        Assert.DoesNotContain(recorder.LastHashEntries!, entry => entry.Name == "metadata");
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
        var (database, recorder) = CreateRouterWithSearchReplyEntries(
            new[]
            {
                ("k1", new[] { ("routeName", "billing"), ("distance", "0.1") }),
                ("k2", new[] { ("routeName", "billing"), ("distance", "0.15") }),
                ("k3", new[] { ("routeName", "support"), ("distance", "0.05") }),
            });
        var router = new SemanticRouter(database, CreateOptions());
        StubRouteThreshold(recorder, router, "billing", 0.1d);

        // billing average (0.125) exceeds its per-route threshold (0.1) and is dropped; support has no
        // per-route threshold and stays within the router default (0.3).
        var matches = await router.RouteManyAsync("question", [1f, 0f], maxResults: 5);

        Assert.Single(matches);
        Assert.Equal("support", matches[0].RouteName);
    }

    [Fact]
    public async Task RouteAsync_AndRouteManyAsync_AgreeWhenPerRouteThresholdRejects()
    {
        // The nearest reference sits at 0.2 — between the route's stricter per-route threshold (0.1) and the
        // router default (0.3). Before the fix, RouteAsync applied only the router default and returned the
        // route while RouteManyAsync correctly rejected it. Both must now reject it identically.
        var (database, recorder) = CreateRouterWithSearchReplyEntries(
            new[]
            {
                ("k1", new[] { ("routeName", "billing"), ("reference", "refund status"), ("distance", "0.2") }),
            });
        var router = new SemanticRouter(database, CreateOptions());
        StubRouteThreshold(recorder, router, "billing", 0.1d);

        var single = await router.RouteAsync("question", [1f, 0f]);
        var many = await router.RouteManyAsync("question", [1f, 0f], maxResults: 5);

        Assert.Null(single);
        Assert.Empty(many);
    }

    [Fact]
    public async Task RouteAsync_SkipsNearestReferenceRejectedByPerRouteThreshold()
    {
        // billing is nearer but its per-route threshold rejects it; RouteAsync returns the nearest reference
        // whose route actually accepts it rather than falling back to the globally nearest reference.
        var (database, recorder) = CreateRouterWithSearchReplyEntries(
            new[]
            {
                ("k1", new[] { ("routeName", "billing"), ("reference", "refund status"), ("distance", "0.12") }),
                ("k2", new[] { ("routeName", "support"), ("reference", "reset password"), ("distance", "0.2") }),
            });
        var router = new SemanticRouter(database, CreateOptions());
        StubRouteThreshold(recorder, router, "billing", 0.1d);

        var match = await router.RouteAsync("question", [1f, 0f]);

        Assert.NotNull(match);
        Assert.Equal("support", match!.RouteName);
        Assert.Equal("reset password", match.Reference);
        Assert.Equal(0.2d, match.Distance, 5);
    }

    [Fact]
    public async Task AddRouteAsync_WithRoute_PersistsThresholdAndMetadataInRouteConfig()
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

        // Exactly one HSET carries the route-level config (threshold + metadata); it is the config key, not
        // a reference. The two reference writes carry the route name but never the threshold or metadata.
        var configWrites = recorder.HashSetCalls
            .Where(entries => entries.Any(entry => entry.Name == "routeThreshold"))
            .ToList();
        var referenceWrites = recorder.HashSetCalls
            .Where(entries => entries.All(entry => entry.Name != "routeThreshold"))
            .ToList();

        var configEntries = Assert.Single(configWrites);
        Assert.Contains(configEntries, entry => entry.Name == "routeThreshold" && entry.Value == "0.2");
        Assert.Contains(configEntries, entry => entry.Name == "metadata" && entry.Value.ToString()!.Contains("finance"));

        Assert.Equal(2, referenceWrites.Count);
        foreach (var entries in referenceWrites)
        {
            Assert.Contains(entries, entry => entry.Name == "routeName" && entry.Value == "billing");
            Assert.DoesNotContain(entries, entry => entry.Name == "metadata");
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

        // Adding references must not write or clear route-level config: no threshold on the references, and
        // no transaction or delete against the config key. This is what keeps a route's threshold stable
        // regardless of which reference happens to be nearest at query time.
        Assert.All(recorder.HashSetCalls, entries =>
            Assert.DoesNotContain(entries, entry => entry.Name == "routeThreshold"));
        Assert.Equal(0, recorder.CreateTransactionCallCount);
        Assert.Empty(recorder.DeletedKeys);
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

        // The returned count reports references removed (2); the per-route config key is deleted too so a
        // future route reusing the name cannot inherit stale config, but it is not counted.
        Assert.Equal(2, deleted);
        Assert.Contains("semantic-router:unit-router:tests:k1", recorder.DeletedKeys.Select(key => key.ToString()));
        Assert.Contains("semantic-router:unit-router:tests:k2", recorder.DeletedKeys.Select(key => key.ToString()));
        Assert.Contains(recorder.DeletedKeys, key => key == router.CreateConfigKey("billing"));
    }

    // Wires the recorder so the given route's config key reports a per-route threshold; other keys keep
    // whatever a previous stub set, defaulting to "no config" (router-default threshold). Composable across
    // calls so several routes can be stubbed independently.
    private static void StubRouteThreshold(
        RecordingDatabaseProxy recorder,
        SemanticRouter router,
        string routeName,
        double threshold)
    {
        var configKey = router.CreateConfigKey(routeName);
        var thresholdValue = (RedisValue)threshold;
        var previous = recorder.HashGetHandler;
        recorder.HashGetHandler = (key, fields) =>
            key == configKey
                ? [.. fields.Select(field => field == "routeThreshold" ? thresholdValue : RedisValue.Null)]
                : previous?.Invoke(key, fields) ?? [.. fields.Select(static _ => RedisValue.Null)];
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

        // Resolves the value of each requested hash field for a given key. Defaults to "field missing" so
        // routing falls back to the router-wide threshold unless a test wires per-route config.
        public Func<RedisKey, RedisValue[], RedisValue[]>? HashGetHandler { get; set; }

        public int ExecuteAsyncCallCount { get; private set; }

        public int HashSetAsyncCallCount { get; private set; }

        public int HashDeleteAsyncCallCount { get; private set; }

        public int CreateTransactionCallCount { get; private set; }

        public HashEntry[]? LastHashEntries { get; private set; }

        public RedisValue[]? LastHashDeleteFields { get; private set; }

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
                nameof(IDatabase.HashDeleteAsync) => HandleHashDeleteAsync(args),
                nameof(IDatabase.HashGetAsync) => HandleHashGetAsync(args),
                nameof(IDatabase.KeyDeleteAsync) => HandleKeyDeleteAsync(args),
                nameof(IDatabase.CreateTransaction) => CreateRecordingTransaction(),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
        }

        // IDatabase exposes HashGetAsync as both a single-field (Task<RedisValue>) and a multi-field
        // (Task<RedisValue[]>) overload; the second argument's type tells them apart.
        private object HandleHashGetAsync(object?[]? args)
        {
            var key = (RedisKey)args![0]!;
            if (args[1] is RedisValue[] fields)
            {
                var values = HashGetHandler?.Invoke(key, fields) ?? [.. fields.Select(static _ => RedisValue.Null)];
                return Task.FromResult(values);
            }

            var field = (RedisValue)args[1]!;
            var single = HashGetHandler?.Invoke(key, [field]);
            return Task.FromResult(single is { Length: > 0 } ? single[0] : RedisValue.Null);
        }

        private ITransaction CreateRecordingTransaction()
        {
            CreateTransactionCallCount++;
            var transaction = DispatchProxy.Create<ITransaction, RecordingTransactionProxy>();
            ((RecordingTransactionProxy)(object)transaction).Parent = this;
            return transaction;
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

        private Task<long> HandleHashDeleteAsync(object?[]? args)
        {
            HashDeleteAsyncCallCount++;
            LastHashDeleteFields = (RedisValue[])args![1]!;
            return Task.FromResult((long)LastHashDeleteFields.Length);
        }

        // IDatabase/ITransaction expose KeyDeleteAsync as a multi-key (Task<long>) and single-key
        // (Task<bool>) overload; return the type that matches the overload actually invoked.
        private object HandleKeyDeleteAsync(object?[]? args)
        {
            if (args![0] is RedisKey[] keys)
            {
                DeletedKeys.AddRange(keys);
                return Task.FromResult((long)keys.Length);
            }

            DeletedKeys.Add((RedisKey)args[0]!);
            return Task.FromResult(true);
        }

        // Records the commands queued inside a MULTI/EXEC onto the parent recorder so a
        // transaction's HSET/HDEL are observed the same way as direct calls. Execution is not
        // deferred because the production code never awaits the queued tasks before ExecuteAsync.
        private class RecordingTransactionProxy : DispatchProxy
        {
            public RecordingDatabaseProxy Parent { get; set; } = null!;

            protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            {
                ArgumentNullException.ThrowIfNull(targetMethod);

                return targetMethod.Name switch
                {
                    nameof(ITransaction.HashSetAsync) => Parent.HandleHashSetAsync(args),
                    nameof(ITransaction.HashDeleteAsync) => Parent.HandleHashDeleteAsync(args),
                    nameof(ITransaction.KeyDeleteAsync) => Parent.HandleKeyDeleteAsync(args),
                    nameof(ITransaction.ExecuteAsync) => Task.FromResult(true),
                    _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this transaction proxy.")
                };
            }
        }
    }
}
