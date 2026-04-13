using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using RedisVlDotNet.Caches;
using RedisVlDotNet.Schema;
using RedisVlDotNet.Vectorizers;
using RedisVlDotNet.Workflows;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Workflows;

public sealed class SemanticMessageHistoryTests
{
    [Fact]
    public async Task CreateAsync_BuildsExpectedSemanticMessageHistorySchema()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new SemanticMessageHistory(database, CreateOptions());

        await history.CreateAsync();

        Assert.Equal("FT.CREATE", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal(
            [
                "semantic-message-history:unit-history:tests",
                "ON",
                "HASH",
                "PREFIX",
                "1",
                "semantic-message-history:unit-history:tests:msg:",
                "SCHEMA",
                "sessionId",
                "TAG",
                "SEPARATOR",
                ",",
                "role",
                "TAG",
                "SEPARATOR",
                ",",
                "content",
                "TEXT",
                "metadata",
                "TEXT",
                "timestamp",
                "NUMERIC",
                "SORTABLE",
                "sequence",
                "NUMERIC",
                "SORTABLE",
                "embedding",
                "VECTOR",
                "FLAT",
                "6",
                "TYPE",
                "FLOAT32",
                "DIM",
                "2",
                "DISTANCE_METRIC",
                "COSINE"
            ],
            recorder.ExecuteAsyncCalls[0].Arguments.Select(static argument => argument?.ToString() ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task AppendAsync_WithEmbeddingGenerator_StoresEmbeddingAndMetadata()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new SemanticMessageHistory(database, CreateOptions());
        var generator = new RecordingEmbeddingGenerator([1f, 0f]);
        var timestamp = DateTimeOffset.Parse("2026-04-13T13:00:00Z");

        recorder.StringIncrementAsyncResult = 7;

        var key = await history.AppendAsync(
            " session-1 ",
            " assistant ",
            " hello world ",
            generator,
            new { sentiment = "positive" },
            timestamp);

        var sessionHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("session-1")));

        Assert.Equal("hello world", generator.LastInput);
        Assert.Equal($"semantic-message-history:unit-history:tests:msg:{sessionHash}:00000000000000000007", key);
        Assert.Equal($"semantic-message-history:unit-history:tests:seq:{sessionHash}", recorder.LastStringIncrementKey);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "sessionId" && entry.Value == "session-1");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "role" && entry.Value == "assistant");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "content" && entry.Value == "hello world");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "metadata" && entry.Value == "{\"sentiment\":\"positive\"}");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "embedding");
    }

    [Fact]
    public async Task GetRelevantAsync_FiltersBySessionRoleAndThreshold()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new SemanticMessageHistory(database, CreateOptions());

        recorder.ExecuteAsyncHandler = (command, _) => command switch
        {
            "FT.SEARCH" => Task.FromResult(
                RedisResult.Create(
                    [
                        RedisResult.Create(2),
                        RedisResult.Create((RedisValue)"semantic-message-history:unit-history:tests:msg:hash:00000000000000000012"),
                        RedisResult.Create(
                            [
                                RedisResult.Create((RedisValue)"sessionId"),
                                RedisResult.Create((RedisValue)"session-1"),
                                RedisResult.Create((RedisValue)"role"),
                                RedisResult.Create((RedisValue)"assistant"),
                                RedisResult.Create((RedisValue)"content"),
                                RedisResult.Create((RedisValue)"refund status"),
                                RedisResult.Create((RedisValue)"metadata"),
                                RedisResult.Create((RedisValue)"{\"turn\":2}"),
                                RedisResult.Create((RedisValue)"timestamp"),
                                RedisResult.Create((RedisValue)"1776085200000"),
                                RedisResult.Create((RedisValue)"distance"),
                                RedisResult.Create((RedisValue)"0.04")
                            ]),
                        RedisResult.Create((RedisValue)"semantic-message-history:unit-history:tests:msg:hash:00000000000000000011"),
                        RedisResult.Create(
                            [
                                RedisResult.Create((RedisValue)"sessionId"),
                                RedisResult.Create((RedisValue)"session-1"),
                                RedisResult.Create((RedisValue)"role"),
                                RedisResult.Create((RedisValue)"assistant"),
                                RedisResult.Create((RedisValue)"content"),
                                RedisResult.Create((RedisValue)"billing help"),
                                RedisResult.Create((RedisValue)"timestamp"),
                                RedisResult.Create((RedisValue)"1776085140000"),
                                RedisResult.Create((RedisValue)"distance"),
                                RedisResult.Create((RedisValue)"0.12")
                            ])
                    ])),
            _ => Task.FromResult(RedisResult.Create((RedisValue)"OK"))
        };

        var matches = await history.GetRelevantAsync("session-1", [1f, 0f], limit: 2, role: "assistant", distanceThreshold: 0.2d);

        Assert.Equal("FT.SEARCH", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal("@sessionId:{session\\-1} @role:{assistant} @embedding:[VECTOR_RANGE 0.2 $vector]=>{$YIELD_DISTANCE_AS: distance}", recorder.ExecuteAsyncCalls[0].Arguments[1]);
        Assert.Contains("SORTBY", recorder.ExecuteAsyncCalls[0].Arguments);
        Assert.Contains("distance", recorder.ExecuteAsyncCalls[0].Arguments);
        Assert.Equal(["refund status", "billing help"], matches.Select(static match => match.Message.Content).ToArray());
        Assert.Equal([12L, 11L], matches.Select(static match => match.Message.Sequence).ToArray());
        Assert.Equal(0.04d, matches[0].Distance, 3);
    }

    [Fact]
    public async Task GetRelevantAsync_WithEmbeddingGenerator_UsesGeneratedEmbedding()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncHandler = (command, _) => command switch
        {
            "FT.SEARCH" => Task.FromResult(
                RedisResult.Create(
                    [
                        RedisResult.Create(1),
                        RedisResult.Create((RedisValue)"semantic-message-history:unit-history:tests:msg:hash:00000000000000000001"),
                        RedisResult.Create(
                            [
                                RedisResult.Create((RedisValue)"sessionId"),
                                RedisResult.Create((RedisValue)"session-1"),
                                RedisResult.Create((RedisValue)"role"),
                                RedisResult.Create((RedisValue)"assistant"),
                                RedisResult.Create((RedisValue)"content"),
                                RedisResult.Create((RedisValue)"stored result"),
                                RedisResult.Create((RedisValue)"timestamp"),
                                RedisResult.Create((RedisValue)"1776085200000"),
                                RedisResult.Create((RedisValue)"distance"),
                                RedisResult.Create((RedisValue)"0.02")
                            ])
                    ])),
            _ => Task.FromResult(RedisResult.Create((RedisValue)"OK"))
        };

        var generator = new RecordingEmbeddingGenerator([1f, 0f]);
        var history = new SemanticMessageHistory(database, CreateOptions());

        var matches = await history.GetRelevantAsync("session-1", "where is my refund?", generator);

        Assert.Equal("where is my refund?", generator.LastInput);
        Assert.Single(matches);
        Assert.Equal("stored result", matches[0].Message.Content);
    }

    [Fact]
    public void Constructor_RejectsNonFloat32VectorFields()
    {
        var attributes = new VectorFieldAttributes(
            VectorAlgorithm.Flat,
            VectorDataType.Float64,
            VectorDistanceMetric.Cosine,
            2);

        Assert.Throws<ArgumentException>(() => new SemanticMessageHistoryOptions("unit-history", attributes, 0.3d));
    }

    [Fact]
    public async Task GetRelevantAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new SemanticMessageHistory(database, CreateOptions());

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            history.GetRelevantAsync("session-1", [1f, 0f], cancellationToken: cancellationTokenSource.Token));
        Assert.Empty(recorder.ExecuteAsyncCalls);
    }

    private static SemanticMessageHistoryOptions CreateOptions() =>
        new("unit-history", CreateVectorAttributes(), 0.3d, "tests");

    private static VectorFieldAttributes CreateVectorAttributes() =>
        new(
            VectorAlgorithm.Flat,
            VectorDataType.Float32,
            VectorDistanceMetric.Cosine,
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

        public long StringIncrementAsyncResult { get; set; } = 1;

        public List<RecordedCommand> ExecuteAsyncCalls { get; } = [];

        public HashEntry[]? LastHashEntries { get; private set; }

        public RedisKey LastStringIncrementKey { get; private set; }

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
                nameof(IDatabase.StringIncrementAsync) => HandleStringIncrementAsync(args),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
        }

        private Task<RedisResult> HandleExecuteAsync(object?[]? args)
        {
            var command = (string)args![0]!;
            var arguments = (object?[]?)args[1] ?? [];
            ExecuteAsyncCalls.Add(new RecordedCommand(command, arguments.Select(static argument => argument!).ToArray()));

            return ExecuteAsyncHandler is not null
                ? ExecuteAsyncHandler(command, args)
                : Task.FromResult(RedisResult.Create((RedisValue)"OK"));
        }

        private Task<bool> HandleHashSetAsync(object?[]? args)
        {
            LastHashEntries = (HashEntry[])args![1]!;
            return Task.FromResult(true);
        }

        private Task<long> HandleStringIncrementAsync(object?[]? args)
        {
            LastStringIncrementKey = (RedisKey)args![0]!;
            return Task.FromResult(StringIncrementAsyncResult);
        }
    }

    private sealed record RecordedCommand(string Command, object[] Arguments);
}
