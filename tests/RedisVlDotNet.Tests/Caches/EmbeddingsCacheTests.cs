using System.Reflection;
using RedisVl.Caches;
using StackExchange.Redis;

namespace RedisVl.Tests.Caches;

public sealed class EmbeddingsCacheTests
{
    [Fact]
    public async Task LookupAsync_ReturnsStoredEntryForExactInputMatch()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));
        var embedding = new[] { 1f, 2f, 3f };

        var stored = await cache.StoreAsync("hello world", embedding);
        var cached = await cache.LookupAsync("hello world");

        Assert.True(stored);
        Assert.NotNull(cached);
        Assert.Equal("hello world", cached!.Input);
        Assert.Null(cached.ModelName);
        Assert.Null(cached.Metadata);
        Assert.Equal(embedding, cached.Embedding);
        Assert.Equal("embeddings:unit-cache:tests:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", cached.Key);
        Assert.Equal("embeddings:unit-cache:tests:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", recorder.LastKey);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Equal(1, recorder.HashGetAllAsyncCallCount);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "model_name" && entry.Value == RedisValue.EmptyString);
    }

    [Fact]
    public async Task LookupAsync_WithModelName_UsesDistinctKeyForSameInput()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));
        var firstEmbedding = new[] { 1f, 2f, 3f };
        var secondEmbedding = new[] { 4f, 5f, 6f };

        var firstStored = await cache.StoreAsync("hello world", "text-embedding-3-small", firstEmbedding);
        var firstKey = recorder.LastKey;
        var secondStored = await cache.StoreAsync("hello world", "text-embedding-3-large", secondEmbedding);
        var secondKey = recorder.LastKey;
        var firstHit = await cache.LookupAsync("hello world", "text-embedding-3-small");
        var firstLookupKey = recorder.LastKey;
        var secondHit = await cache.LookupAsync("hello world", "text-embedding-3-large");
        var secondLookupKey = recorder.LastKey;
        var legacyMiss = await cache.LookupAsync("hello world");

        Assert.True(firstStored);
        Assert.True(secondStored);
        Assert.NotEqual(firstKey, secondKey);
        Assert.Equal(firstKey, firstLookupKey);
        Assert.Equal(secondKey, secondLookupKey);
        Assert.Equal("text-embedding-3-small", firstHit!.ModelName);
        Assert.Equal(firstEmbedding, firstHit.Embedding);
        Assert.Equal("text-embedding-3-large", secondHit!.ModelName);
        Assert.Equal(secondEmbedding, secondHit.Embedding);
        Assert.Null(legacyMiss);
    }

    [Fact]
    public async Task LookupAsync_RoundTripsModelAndMetadata()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        var stored = await cache.StoreAsync(
            "hello world",
            "text-embedding-3-small",
            [1f, 2f, 3f],
            metadata: new { source = "faq", tenant = "team-a" });
        var cached = await cache.LookupAsync("hello world", "text-embedding-3-small");

        Assert.True(stored);
        Assert.NotNull(cached);
        Assert.Equal("hello world", cached!.Input);
        Assert.Equal("text-embedding-3-small", cached.ModelName);
        Assert.Equal("{\"source\":\"faq\",\"tenant\":\"team-a\"}", cached.Metadata);
        Assert.Equal([1f, 2f, 3f], cached.Embedding);
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
            new HashEntry("model_name", RedisValue.EmptyString),
            new HashEntry("embedding", EmbeddingsCache.EncodeFloat32([1f, 2f]))
        ];

        var cached = await cache.LookupAsync("prompt");

        Assert.Null(cached);
    }

    [Fact]
    public void CreateKey_WithoutModelName_PreservesLegacyHash()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        var legacyKey = cache.CreateKey("hello world");

        Assert.Equal("embeddings:unit-cache:tests:b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", legacyKey);
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

        public HashEntry[]? LastHashEntries { get; private set; }

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
            LastHashEntries = value;
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
