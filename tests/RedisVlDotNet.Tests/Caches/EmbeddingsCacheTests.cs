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
    public async Task SetAsync_ReturnsStoredEntryAndRedisKey()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        var stored = await cache.SetAsync(
            "hello world",
            "text-embedding-3-small",
            [1f, 2f, 3f],
            metadata: new { source = "faq" });

        Assert.Equal("hello world", stored.Input);
        Assert.Equal("text-embedding-3-small", stored.ModelName);
        Assert.Equal([1f, 2f, 3f], stored.Embedding);
        Assert.Equal("{\"source\":\"faq\"}", stored.Metadata);
        Assert.Equal(recorder.LastKey, stored.Key);
    }

    [Fact]
    public async Task GetAsync_UsesPythonParityAlias()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        await cache.SetAsync("hello world", "text-embedding-3-small", [1f, 2f, 3f]);
        var cached = await cache.GetAsync("hello world", "text-embedding-3-small");

        Assert.NotNull(cached);
        Assert.Equal("hello world", cached!.Input);
        Assert.Equal("text-embedding-3-small", cached.ModelName);
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsStoredEntryForDirectKeyHit()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        var stored = await cache.SetAsync("hello world", "text-embedding-3-small", [1f, 2f, 3f]);
        var cached = await cache.GetByKeyAsync(stored.Key!);

        Assert.NotNull(cached);
        Assert.Equal(stored.Key, cached!.Key);
        Assert.Equal("hello world", cached.Input);
        Assert.Equal("text-embedding-3-small", cached.ModelName);
        Assert.Equal([1f, 2f, 3f], cached.Embedding);
        Assert.Equal(stored.Key, recorder.LastKey);
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsMissForUnknownKey()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        var cached = await cache.GetByKeyAsync("embeddings:unit-cache:tests:missing");

        Assert.Null(cached);
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
    public async Task StoreAsync_UsesCacheLevelTimeToLiveByDefault()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(
            database,
            new EmbeddingsCacheOptions("unit-cache", "tests", TimeSpan.FromMinutes(10)));

        var stored = await cache.StoreAsync("hello world", [1f, 2f, 3f]);

        Assert.True(stored);
        Assert.Equal(1, recorder.KeyExpireAsyncCallCount);
        Assert.Equal(TimeSpan.FromMinutes(10), recorder.LastExpiry);
    }

    [Fact]
    public async Task SetAsync_PerCallTimeToLiveOverridesCacheLevelTimeToLive()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(
            database,
            new EmbeddingsCacheOptions("unit-cache", "tests", TimeSpan.FromMinutes(10)));

        var stored = await cache.SetAsync(
            "hello world",
            "text-embedding-3-small",
            [1f, 2f, 3f],
            metadata: new { source = "faq" },
            timeToLive: TimeSpan.FromSeconds(30));

        Assert.Equal("text-embedding-3-small", stored.ModelName);
        Assert.Equal(1, recorder.KeyExpireAsyncCallCount);
        Assert.Equal(TimeSpan.FromSeconds(30), recorder.LastExpiry);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForStoredSemanticKey()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        await cache.SetAsync("hello world", "text-embedding-3-small", [1f, 2f, 3f]);

        var exists = await cache.ExistsAsync("hello world", "text-embedding-3-small");

        Assert.True(exists);
        Assert.Equal(1, recorder.KeyExistsAsyncCallCount);
        Assert.Equal(cache.CreateKey("hello world", "text-embedding-3-small"), recorder.LastKey);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForSemanticMiss()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        await cache.SetAsync("hello world", "text-embedding-3-small", [1f, 2f, 3f]);

        var exists = await cache.ExistsAsync("hello world", "text-embedding-3-large");

        Assert.False(exists);
        Assert.Equal(1, recorder.KeyExistsAsyncCallCount);
    }

    [Fact]
    public async Task ExistsByKeyAsync_ReturnsTrueForStoredDirectKey()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));
        var stored = await cache.SetAsync("hello world", [1f, 2f, 3f]);

        var exists = await cache.ExistsByKeyAsync(stored.Key!);

        Assert.True(exists);
    }

    [Fact]
    public async Task ExistsByKeyAsync_ReturnsFalseForUnknownDirectKey()
    {
        var (database, _) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        var exists = await cache.ExistsByKeyAsync("embeddings:unit-cache:tests:missing");

        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsTrueForStoredSemanticKey()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        await cache.SetAsync("hello world", "text-embedding-3-small", [1f, 2f, 3f]);

        var deleted = await cache.DeleteAsync("hello world", "text-embedding-3-small");

        Assert.True(deleted);
        Assert.Equal(1, recorder.KeyDeleteAsyncCallCount);
        Assert.Equal(cache.CreateKey("hello world", "text-embedding-3-small"), recorder.LastKey);
        Assert.False(recorder.StoredValues.ContainsKey(cache.CreateKey("hello world", "text-embedding-3-small")));
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForSemanticMiss()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));

        await cache.SetAsync("hello world", "text-embedding-3-small", [1f, 2f, 3f]);

        var deleted = await cache.DeleteAsync("hello world", "text-embedding-3-large");

        Assert.False(deleted);
        Assert.Equal(1, recorder.KeyDeleteAsyncCallCount);
    }

    [Fact]
    public async Task DeleteByKeyAsync_ReturnsFalseAfterRepeatedDelete()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var cache = new EmbeddingsCache(database, new EmbeddingsCacheOptions("unit-cache", "tests"));
        var stored = await cache.SetAsync("hello world", [1f, 2f, 3f]);

        var firstDelete = await cache.DeleteByKeyAsync(stored.Key!);
        var secondDelete = await cache.DeleteByKeyAsync(stored.Key!);

        Assert.True(firstDelete);
        Assert.False(secondDelete);
        Assert.Equal(2, recorder.KeyDeleteAsyncCallCount);
        Assert.Equal(stored.Key, recorder.LastKey);
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

        public int KeyExpireAsyncCallCount { get; private set; }

        public int KeyExistsAsyncCallCount { get; private set; }

        public int KeyDeleteAsyncCallCount { get; private set; }

        public RedisKey? LastKey { get; private set; }

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
                nameof(IDatabase.HashSetAsync) => HandleHashSetAsync(args),
                nameof(IDatabase.HashGetAllAsync) => HandleHashGetAllAsync(args),
                nameof(IDatabase.KeyExpireAsync) => HandleKeyExpireAsync(args),
                nameof(IDatabase.KeyExistsAsync) => HandleKeyExistsAsync(args),
                nameof(IDatabase.KeyDeleteAsync) => HandleKeyDeleteAsync(args),
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

        private Task<bool> HandleKeyExpireAsync(object?[]? args)
        {
            KeyExpireAsyncCallCount++;
            LastKey = (RedisKey)args![0]!;
            LastExpiry = (TimeSpan?)args[1];
            return Task.FromResult(true);
        }

        private Task<bool> HandleKeyExistsAsync(object?[]? args)
        {
            KeyExistsAsyncCallCount++;
            var key = (RedisKey)args![0]!;
            LastKey = key;
            return Task.FromResult(StoredValues.ContainsKey(key));
        }

        private Task<bool> HandleKeyDeleteAsync(object?[]? args)
        {
            KeyDeleteAsyncCallCount++;
            var key = (RedisKey)args![0]!;
            LastKey = key;
            return Task.FromResult(StoredValues.Remove(key));
        }
    }
}
