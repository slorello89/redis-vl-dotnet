using System.Reflection;
using RedisVlDotNet.Caches;
using RedisVlDotNet.Schema;
using RedisVlDotNet.Vectorizers;
using RedisVlDotNet.Workflows;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Workflows;

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
            return Task.FromResult(true);
        }
    }
}
