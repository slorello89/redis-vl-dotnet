using System.Reflection;
using RedisVL.Caches;
using RedisVL.Filters;
using RedisVL.Schema;
using RedisVL.Vectorizers;
using StackExchange.Redis;

namespace RedisVL.Tests.Caches;

public sealed class SemanticCacheTests
{
    [Fact]
    public async Task CheckAsync_WithEmbeddingGenerator_UsesGeneratedEmbeddingAndReturnsNearestHit()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncHandler = (command, _) => command switch
        {
            "FT.SEARCH" => Task.FromResult(
                RedisResult.Create(
                    [
                        RedisResult.Create(1),
                        RedisResult.Create((RedisValue)"semantic:unit-cache:tests:key"),
                        RedisResult.Create(
                            [
                                RedisResult.Create((RedisValue)"prompt"),
                                RedisResult.Create((RedisValue)"stored prompt"),
                                RedisResult.Create((RedisValue)"response"),
                                RedisResult.Create((RedisValue)"cached response"),
                                RedisResult.Create((RedisValue)"metadata"),
                                RedisResult.Create((RedisValue)"{\"tenant\":\"team-a\"}"),
                                RedisResult.Create((RedisValue)"distance"),
                                RedisResult.Create((RedisValue)"0.12")
                            ])
                    ])),
            _ => Task.FromResult(RedisResult.Create((RedisValue)"OK"))
        };

        var generator = new RecordingEmbeddingGenerator([1f, 0f]);
        var cache = new SemanticCache(database, CreateOptions());

        var hit = await cache.CheckAsync("new prompt", generator);

        Assert.NotNull(hit);
        Assert.Equal("new prompt", generator.LastInput);
        Assert.Equal("stored prompt", hit!.Prompt);
        Assert.Equal("cached response", hit.Response);
        Assert.Equal("{\"tenant\":\"team-a\"}", hit.Metadata);
        Assert.Equal(0.12d, hit.Distance, 3);
    }

    [Fact]
    public async Task CheckAsync_WithFilter_UsesVectorRangeFilterQuery()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncHandler = (_, _) => Task.FromResult(RedisResult.Create([RedisResult.Create(0)]));
        var cache = new SemanticCache(database, CreateOptions());

        _ = await cache.CheckAsync("prompt", [1f, 0f], Filter.Tag("tenant").Eq("team-a"));

        Assert.NotNull(recorder.LastExecuteArguments);
        Assert.Equal("FT.SEARCH", recorder.LastExecuteCommand);
        Assert.Contains(
            recorder.LastExecuteArguments!.OfType<string>(),
            argument => argument.Contains("@tenant:{team\\-a}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CheckAsync_WithFilterAndNoConfiguredFilterFields_Throws()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(
            database,
            new SemanticCacheOptions("unit-cache", CreateVectorAttributes(), 0.3d, "tests"));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.CheckAsync("prompt", [1f, 0f], Filter.Tag("tenant").Eq("team-a")));
    }

    [Fact]
    public async Task StoreAsync_WithMetadataAndFilters_WritesHashAndExpiration()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(
            database,
            new SemanticCacheOptions(
                "unit-cache",
                CreateVectorAttributes(),
                0.3d,
                "tests",
                TimeSpan.FromMinutes(5),
                filterableFields:
                [
                    new TagFieldDefinition("tenant"),
                    new NumericFieldDefinition("temperature"),
                    new TextFieldDefinition("promptTemplate")
                ]));

        var key = await cache.StoreAsync(
            "hello world",
            "cached response",
            [1f, 2f],
            metadata: new { source = "faq" },
            filterValues: new Dictionary<string, object?>
            {
                ["tenant"] = "team-a",
                ["temperature"] = 0.2d,
                ["promptTemplate"] = "support"
            });

        Assert.StartsWith("semantic:unit-cache:tests:", key, StringComparison.Ordinal);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Equal(1, recorder.KeyExpireAsyncCallCount);
        // The HSET and EXPIRE are bundled into one MULTI/EXEC so a cancellation or dropped
        // connection between them can never leave the entry without its TTL.
        Assert.Equal(1, recorder.CreateTransactionCallCount);
        Assert.Equal(TimeSpan.FromMinutes(5), recorder.LastExpiry);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "response" && entry.Value == "cached response");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "metadata" && entry.Value == "{\"source\":\"faq\"}");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "tenant" && entry.Value == "team-a");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "temperature" && entry.Value == "0.2");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "promptTemplate" && entry.Value == "support");
    }

    [Fact]
    public async Task StoreAsync_WithUndefinedFilterField_Throws()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.StoreAsync(
                "prompt",
                "response",
                [1f, 0f],
                filterValues: new Dictionary<string, object?>
                {
                    ["unknown"] = "value"
                }));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
    }

    [Fact]
    public async Task StoreAsync_WithInvalidNumericFilterValue_Throws()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.StoreAsync(
                "prompt",
                "response",
                [1f, 0f],
                filterValues: new Dictionary<string, object?>
                {
                    ["temperature"] = "hot"
                }));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
    }

    [Fact]
    public async Task StoreAsync_WithCancelledToken_DoesNotWriteToRedis()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            cache.StoreAsync("prompt", "response", [1f, 0f], cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
    }

    [Fact]
    public async Task StoreAsync_WithEmbeddingLengthNotMatchingDimensions_ThrowsAndDoesNotWrite()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        // The schema declares two dimensions; a three-value embedding would be silently rejected by
        // RediSearch on write, so the store must fail loudly before dispatching any command.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.StoreAsync("prompt", "response", [1f, 2f, 3f]));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
        Assert.Equal(0, recorder.CreateTransactionCallCount);
    }

    [Fact]
    public async Task StoreAsync_WithoutMetadata_ClearsStaleMetadataAtomically()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        await cache.StoreAsync("prompt", "response", [1f, 0f]);

        // No metadata this time, so the field is deleted in the same transaction as the write to
        // avoid an HSET-merge pairing the new response with a previous entry's metadata.
        Assert.Equal(1, recorder.CreateTransactionCallCount);
        Assert.Equal(1, recorder.HashDeleteAsyncCallCount);
        Assert.Contains(recorder.LastHashDeleteFields!, field => field == "metadata");
        Assert.DoesNotContain(recorder.LastHashEntries!, entry => entry.Name == "metadata");
    }

    [Fact]
    public async Task StoreAsync_WithMetadataAndNoTtl_WritesWithoutClearing()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        await cache.StoreAsync("prompt", "response", [1f, 0f], metadata: new { source = "faq" });

        Assert.Equal(0, recorder.HashDeleteAsyncCallCount);
        Assert.Equal(0, recorder.CreateTransactionCallCount);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "metadata");
    }

    [Fact]
    public void SemanticCacheOptions_RejectUnsupportedFilterableFieldDefinitions()
    {
        Assert.Throws<ArgumentException>(() =>
            new SemanticCacheOptions(
                "unit-cache",
                CreateVectorAttributes(),
                0.3d,
                filterableFields: [new GeoFieldDefinition("location")]));

        Assert.Throws<ArgumentException>(() =>
            new SemanticCacheOptions(
                "unit-cache",
                CreateVectorAttributes(),
                0.3d,
                filterableFields: [new TagFieldDefinition("tenant", alias: "tenantAlias")]));
    }

    [Fact]
    public async Task CheckTopKAsync_ReturnsHitsOrderedNearestFirst()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncHandler = (command, _) => command switch
        {
            "FT.SEARCH" => Task.FromResult(
                RedisResult.Create(
                [
                    RedisResult.Create(2),
                    RedisResult.Create((RedisValue)"semantic:unit-cache:tests:k1"),
                    RedisResult.Create(
                    [
                        RedisResult.Create((RedisValue)"prompt"),
                        RedisResult.Create((RedisValue)"first"),
                        RedisResult.Create((RedisValue)"response"),
                        RedisResult.Create((RedisValue)"resp-1"),
                        RedisResult.Create((RedisValue)"distance"),
                        RedisResult.Create((RedisValue)"0.05")
                    ]),
                    RedisResult.Create((RedisValue)"semantic:unit-cache:tests:k2"),
                    RedisResult.Create(
                    [
                        RedisResult.Create((RedisValue)"prompt"),
                        RedisResult.Create((RedisValue)"second"),
                        RedisResult.Create((RedisValue)"response"),
                        RedisResult.Create((RedisValue)"resp-2"),
                        RedisResult.Create((RedisValue)"distance"),
                        RedisResult.Create((RedisValue)"0.2")
                    ])
                ])),
            _ => Task.FromResult(RedisResult.Create((RedisValue)"OK"))
        };
        var cache = new SemanticCache(database, CreateOptions());

        var hits = await cache.CheckTopKAsync("prompt", [1f, 0f], topK: 5);

        Assert.Equal(2, hits.Count);
        Assert.Equal("resp-1", hits[0].Response);
        Assert.Equal("resp-2", hits[1].Response);
        Assert.True(hits[0].Distance < hits[1].Distance);
    }

    [Fact]
    public async Task CheckTopKAsync_WithNonPositiveTopK_Throws()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.CheckTopKAsync("prompt", [1f, 0f], 0));
    }

    [Fact]
    public async Task Statistics_TrackHitsMissesAndRate_WhenEnabled()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var hitNext = true;
        recorder.ExecuteAsyncHandler = (command, _) =>
            command == "FT.SEARCH"
                ? Task.FromResult(hitNext ? SingleHitResult() : MissResult())
                : Task.FromResult(RedisResult.Create((RedisValue)"OK"));
        var cache = new SemanticCache(database, CreateOptions(trackStatistics: true));

        hitNext = true;
        await cache.CheckAsync("p", [1f, 0f]);
        await cache.CheckAsync("p", [1f, 0f]);
        hitNext = false;
        await cache.CheckAsync("p", [0f, 1f]);

        Assert.Equal(2, cache.HitCount);
        Assert.Equal(1, cache.MissCount);
        Assert.Equal(2d / 3d, cache.HitRate, 5);

        cache.ResetStatistics();
        Assert.Equal(0, cache.HitCount);
        Assert.Equal(0, cache.MissCount);
        Assert.Equal(0d, cache.HitRate);
    }

    [Fact]
    public async Task Statistics_StayZero_WhenDisabled()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncHandler = (_, _) => Task.FromResult(SingleHitResult());
        var cache = new SemanticCache(database, CreateOptions());

        await cache.CheckAsync("p", [1f, 0f]);

        Assert.Equal(0, cache.HitCount);
        Assert.Equal(0, cache.MissCount);
        Assert.Equal(0d, cache.HitRate);
    }

    [Fact]
    public async Task StoreManyAsync_WritesEachEntryAndReturnsAlignedKeys()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        var keys = await cache.StoreManyAsync(
        [
            new SemanticCacheStoreRequest("p1", "r1", [1f, 0f]),
            new SemanticCacheStoreRequest("p2", "r2", [0f, 1f])
        ]);

        Assert.Equal(2, keys.Count);
        Assert.Equal(2, recorder.HashSetAsyncCallCount);
        Assert.All(keys, key => Assert.StartsWith("semantic:unit-cache:tests:", key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task StoreManyAsync_WithoutEmbedding_Throws()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.StoreManyAsync([new SemanticCacheStoreRequest("p", "r")]));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
    }

    [Fact]
    public async Task CheckManyAsync_ReturnsAlignedHitsAndMisses()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var calls = 0;
        recorder.ExecuteAsyncHandler = (command, _) =>
        {
            if (command != "FT.SEARCH")
            {
                return Task.FromResult(RedisResult.Create((RedisValue)"OK"));
            }

            calls++;
            return Task.FromResult(calls == 1 ? SingleHitResult() : MissResult());
        };
        var cache = new SemanticCache(database, CreateOptions());

        var results = await cache.CheckManyAsync(
        [
            new SemanticCacheCheckRequest("p1", [1f, 0f]),
            new SemanticCacheCheckRequest("p2", [0f, 1f])
        ]);

        Assert.Equal(2, results.Count);
        Assert.NotNull(results[0]);
        Assert.Null(results[1]);
    }

    [Fact]
    public async Task UpdateAsync_ExistingKey_WritesProvidedFieldsAndRefreshesTtl()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.KeyExistsResult = true;
        var cache = new SemanticCache(database, CreateOptions(timeToLive: TimeSpan.FromMinutes(5)));

        var updated = await cache.UpdateAsync(
            "semantic:unit-cache:tests:key",
            response: "new response",
            metadata: new { v = 2 });

        Assert.True(updated);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "response" && entry.Value == "new response");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "metadata" && entry.Value == "{\"v\":2}");
        Assert.Equal(1, recorder.KeyExpireAsyncCallCount);
    }

    [Fact]
    public async Task UpdateAsync_MissingKey_ReturnsFalseWithoutWriting()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.KeyExistsResult = false;
        var cache = new SemanticCache(database, CreateOptions());

        var updated = await cache.UpdateAsync("semantic:unit-cache:tests:missing", response: "x");

        Assert.False(updated);
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
    }

    [Fact]
    public async Task UpdateAsync_WithNothingToUpdate_Throws()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        await Assert.ThrowsAsync<ArgumentException>(() => cache.UpdateAsync("key"));
    }

    private static SemanticCacheOptions CreateOptions(bool trackStatistics = false, TimeSpan? timeToLive = null) =>
        new(
            "unit-cache",
            CreateVectorAttributes(),
            0.3d,
            "tests",
            timeToLive,
            filterableFields:
            [
                new TagFieldDefinition("tenant"),
                new NumericFieldDefinition("temperature"),
                new TextFieldDefinition("promptTemplate")
            ],
            trackStatistics: trackStatistics);

    private static RedisResult SingleHitResult(string prompt = "stored", string response = "cached", string distance = "0.1") =>
        RedisResult.Create(
        [
            RedisResult.Create(1),
            RedisResult.Create((RedisValue)"semantic:unit-cache:tests:key"),
            RedisResult.Create(
            [
                RedisResult.Create((RedisValue)"prompt"),
                RedisResult.Create((RedisValue)prompt),
                RedisResult.Create((RedisValue)"response"),
                RedisResult.Create((RedisValue)response),
                RedisResult.Create((RedisValue)"distance"),
                RedisResult.Create((RedisValue)distance)
            ])
        ]);

    private static RedisResult MissResult() => RedisResult.Create([RedisResult.Create(0)]);

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

        public string? LastExecuteCommand { get; private set; }

        public object[]? LastExecuteArguments { get; private set; }

        public int HashSetAsyncCallCount { get; private set; }

        public int KeyExpireAsyncCallCount { get; private set; }

        public int KeyExistsAsyncCallCount { get; private set; }

        public int HashDeleteAsyncCallCount { get; private set; }

        public int CreateTransactionCallCount { get; private set; }

        public bool KeyExistsResult { get; set; } = true;

        public HashEntry[]? LastHashEntries { get; private set; }

        public RedisValue[]? LastHashDeleteFields { get; private set; }

        public TimeSpan? LastExpiry { get; private set; }

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
                nameof(IDatabase.KeyExpireAsync) => HandleKeyExpireAsync(args),
                nameof(IDatabase.KeyExistsAsync) => HandleKeyExistsAsync(),
                nameof(IDatabase.CreateTransaction) => CreateRecordingTransaction(),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
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
            LastExecuteCommand = (string)args![0]!;
            LastExecuteArguments = (object[]?)args[1];
            return ExecuteAsyncHandler is not null
                ? ExecuteAsyncHandler(LastExecuteCommand, args)
                : Task.FromResult(RedisResult.Create((RedisValue)"OK"));
        }

        private Task<bool> HandleHashSetAsync(object?[]? args)
        {
            HashSetAsyncCallCount++;
            LastHashEntries = (HashEntry[])args![1]!;
            return Task.FromResult(true);
        }

        private Task<long> HandleHashDeleteAsync(object?[]? args)
        {
            HashDeleteAsyncCallCount++;
            LastHashDeleteFields = (RedisValue[])args![1]!;
            return Task.FromResult((long)LastHashDeleteFields.Length);
        }

        private Task<bool> HandleKeyExpireAsync(object?[]? args)
        {
            KeyExpireAsyncCallCount++;
            LastExpiry = (TimeSpan?)args![1]!;
            return Task.FromResult(true);
        }

        private Task<bool> HandleKeyExistsAsync()
        {
            KeyExistsAsyncCallCount++;
            return Task.FromResult(KeyExistsResult);
        }

        // Records the commands queued inside a MULTI/EXEC onto the parent recorder so tests observe
        // a transaction's HSET/HDEL/EXPIRE the same way they observe direct calls. Execution is not
        // deferred (as a real transaction would be) because the production code never awaits the
        // queued command tasks before ExecuteAsync.
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
                    nameof(ITransaction.KeyExpireAsync) => Parent.HandleKeyExpireAsync(args),
                    nameof(ITransaction.ExecuteAsync) => Task.FromResult(true),
                    _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this transaction proxy.")
                };
            }
        }
    }
}
