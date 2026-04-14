using System.Reflection;
using RedisVl.Caches;
using StackExchange.Redis;

namespace RedisVl.Tests.Caches;

public sealed class EmbeddingsCacheTests
{
    [Fact]
    public async Task LookupAsync_ReturnsStoredEmbeddingForExactInputMatch()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));
        var embedding = new[] { 1f, 2f, 3f };

        var stored = await cache.StoreAsync("hello world", embedding);
        var cached = await cache.LookupAsync("hello world");

        Assert.True(stored);
        Assert.NotNull(cached);
        Assert.Equal(embedding, cached);
        Assert.Equal("embeddings:unit-cache:tests:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", recorder.LastKey);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Equal(1, recorder.HashGetAllAsyncCallCount);
    }

    [Fact]
    public async Task LookupAsync_ReturnsMissWhenStoredPayloadInputDoesNotMatch()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache"));
        var key = cache.CreateKey("prompt");

        recorder.StoredValues[key] =
        [
            new HashEntry("input", "other"),
            new HashEntry("embedding", EmbeddingsCache.EncodeFloat32([1f, 2f]))
        ];

        var cached = await cache.LookupAsync("prompt");

        Assert.Null(cached);
    }

    [Fact]
    public async Task StoreAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache"));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            cache.StoreAsync("prompt", [1f, 2f], cancellationTokenSource.Token));
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
    }

    [Fact]
    public async Task LookupAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache"));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            cache.LookupAsync("prompt", cancellationTokenSource.Token));
        Assert.Equal(0, recorder.HashGetAllAsyncCallCount);
    }

    private class RecordingDatabaseProxy : DispatchProxy
    {
        public Dictionary<RedisKey, HashEntry[]> StoredValues { get; } = [];

        public int HashSetAsyncCallCount { get; private set; }

        public int HashGetAllAsyncCallCount { get; private set; }

        public RedisKey? LastKey { get; private set; }

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
                nameof(IDatabase.HashSetAsync) => HandleHashSetAsync(args),
                nameof(IDatabase.HashGetAllAsync) => HandleHashGetAllAsync(args),
                nameof(IDatabase.KeyExpireAsync) => Task.FromResult(true),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
        }

        private Task<bool> HandleHashSetAsync(object?[]? args)
        {
            HashSetAsyncCallCount++;

            var key = (RedisKey)args![0]!;
            var value = (HashEntry[])args[1]!;

            LastKey = key;
            StoredValues[key] = value;
            return Task.FromResult(true);
        }

        private Task<HashEntry[]> HandleHashGetAllAsync(object?[]? args)
        {
            HashGetAllAsyncCallCount++;

            var key = (RedisKey)args![0]!;
            LastKey = key;
            return Task.FromResult(StoredValues.TryGetValue(key, out var value) ? value : []);
        }
    }
}
