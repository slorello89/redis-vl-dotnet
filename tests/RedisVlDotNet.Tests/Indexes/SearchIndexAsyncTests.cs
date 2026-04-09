using System.Reflection;
using RedisVlDotNet.Filters;
using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Indexes;

public sealed class SearchIndexAsyncTests
{
    [Fact]
    public async Task CreateAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("cancel-create"));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.CreateAsync(cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task SearchAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateVectorSchema("cancel-search"));
        var query = VectorQuery.FromFloat32("embedding", [1f, 0f], 1);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.SearchAsync(query, cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task FetchHashByKeyAsync_WithCancelledToken_DoesNotReadFromRedis()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("cancel-fetch"));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.FetchHashByKeyAsync<HashMovieDocument>("movie:1", cancellationTokenSource.Token));
        Assert.Equal(0, recorder.HashGetAllAsyncCallCount);
    }

    [Fact]
    public async Task LoadHashAsync_CancelsBetweenBatchDocuments()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("cancel-batch"));

        using var cancellationTokenSource = new CancellationTokenSource();
        recorder.OnHashSetAsync = (_, _) =>
        {
            cancellationTokenSource.Cancel();
            return Task.FromResult(true);
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.LoadHashAsync(
            [
                new HashMovieDocument("1", "Heat", 1995, "crime"),
                new HashMovieDocument("2", "Thief", 1981, "crime")
            ],
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal(1, recorder.HashSetAsyncCallCount);
    }

    private static SearchSchema CreateHashSchema(string token) =>
        new(
            new IndexDefinition($"hash-{token}", $"movie:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);

    private static SearchSchema CreateVectorSchema(string token) =>
        new(
            new IndexDefinition($"vector-{token}", $"vector:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private class RecordingDatabaseProxy : DispatchProxy
    {
        public int ExecuteAsyncCallCount { get; private set; }

        public int HashGetAllAsyncCallCount { get; private set; }

        public int HashSetAsyncCallCount { get; private set; }

        public Func<RedisKey, HashEntry[], Task<bool>>? OnHashSetAsync { get; set; }

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
                nameof(IDatabase.HashGetAllAsync) => HandleHashGetAllAsync(),
                nameof(IDatabase.HashSetAsync) => HandleHashSetAsync(args),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
        }

        private Task<RedisResult> HandleExecuteAsync(object?[]? args)
        {
            ExecuteAsyncCallCount++;
            return Task.FromResult(RedisResult.Create((RedisValue)"OK"));
        }

        private Task<HashEntry[]> HandleHashGetAllAsync()
        {
            HashGetAllAsyncCallCount++;
            return Task.FromResult(Array.Empty<HashEntry>());
        }

        private Task<bool> HandleHashSetAsync(object?[]? args)
        {
            HashSetAsyncCallCount++;

            if (OnHashSetAsync is not null)
            {
                return OnHashSetAsync((RedisKey)args![0]!, (HashEntry[])args[1]!);
            }

            return Task.FromResult(true);
        }
    }
}
