using System.Reflection;
using RedisVlDotNet.Caches;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Caches;

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
        Assert.Equal(0.12d, hit.Distance, 3);
    }

    [Fact]
    public async Task StoreAsync_WithTimeToLive_WritesHashAndExpiration()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(
            database,
            new SemanticCacheOptions(
                "unit-cache",
                CreateVectorAttributes(),
                0.3d,
                "tests",
                TimeSpan.FromMinutes(5)));

        var key = await cache.StoreAsync("hello world", "cached response", [1f, 2f]);

        Assert.Equal("semantic:unit-cache:tests:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", key);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Equal(1, recorder.KeyExpireAsyncCallCount);
        Assert.Equal(TimeSpan.FromMinutes(5), recorder.LastExpiry);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "response" && entry.Value == "cached response");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "embedding");
    }

    [Fact]
    public async Task StoreAsync_WithCancelledToken_DoesNotWriteToRedis()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new SemanticCache(database, CreateOptions());

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            cache.StoreAsync("prompt", "response", [1f, 0f], cancellationTokenSource.Token));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
    }

    private static SemanticCacheOptions CreateOptions() =>
        new("unit-cache", CreateVectorAttributes(), 0.3d, "tests");

    private static VectorFieldAttributes CreateVectorAttributes() =>
        new(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.L2,
            2);

    private sealed class RecordingEmbeddingGenerator(float[] embedding) : ITextEmbeddingGenerator
    {
        public string? LastInput { get; private set; }

        public Task<float[]> GenerateAsync(string input, CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(embedding);
        }
    }

    private class RecordingDatabaseProxy : DispatchProxy
    {
        public Func<string, object?[]?, Task<RedisResult>>? ExecuteAsyncHandler { get; set; }

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
            var command = (string)args![0]!;
            return ExecuteAsyncHandler is not null
                ? ExecuteAsyncHandler(command, args)
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
