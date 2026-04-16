using System.Reflection;
using RedisVl.Caches;
using RedisVl.Filters;
using RedisVl.Schema;
using RedisVl.Vectorizers;
using StackExchange.Redis;

namespace RedisVl.Tests.Caches;

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

    private static SemanticCacheOptions CreateOptions() =>
        new(
            "unit-cache",
            CreateVectorAttributes(),
            0.3d,
            "tests",
            filterableFields:
            [
                new TagFieldDefinition("tenant"),
                new NumericFieldDefinition("temperature"),
                new TextFieldDefinition("promptTemplate")
            ]);

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

        public HashEntry[]? LastHashEntries { get; private set; }

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
                nameof(IDatabase.KeyExpireAsync) => HandleKeyExpireAsync(args),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
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

        private Task<bool> HandleKeyExpireAsync(object?[]? args)
        {
            KeyExpireAsyncCallCount++;
            LastExpiry = (TimeSpan?)args![1]!;
            return Task.FromResult(true);
        }
    }
}
