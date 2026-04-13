using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using RedisVlDotNet.Workflows;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Workflows;

public sealed class MessageHistoryTests
{
    [Fact]
    public async Task CreateAsync_BuildsExpectedMessageHistorySchema()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new MessageHistory(database, CreateOptions());

        await history.CreateAsync();

        Assert.Equal("FT.CREATE", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal(
            [
                "message-history:unit-history:tests",
                "ON",
                "HASH",
                "PREFIX",
                "1",
                "message-history:unit-history:tests:msg:",
                "SEPARATOR",
                ":",
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
                "SORTABLE"
            ],
            recorder.ExecuteAsyncCalls[0].Arguments.Select(static argument => argument?.ToString() ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task AppendAsync_StoresSequenceTimestampAndSerializedMetadata()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new MessageHistory(database, CreateOptions());
        var timestamp = DateTimeOffset.Parse("2026-04-13T13:00:00Z");

        recorder.StringIncrementAsyncResult = 7;

        var key = await history.AppendAsync(
            " session-1 ",
            " assistant ",
            " hello world ",
            new { sentiment = "positive" },
            timestamp);

        var sessionHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("session-1")));

        Assert.Equal($"message-history:unit-history:tests:msg:{sessionHash}:00000000000000000007", key);
        Assert.Equal($"message-history:unit-history:tests:seq:{sessionHash}", recorder.LastStringIncrementKey);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "sessionId" && entry.Value == "session-1");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "role" && entry.Value == "assistant");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "content" && entry.Value == "hello world");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "timestamp" && entry.Value == "1776085200000");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "sequence" && entry.Value == "7");
        Assert.Contains(recorder.LastHashEntries!, entry => entry.Name == "metadata" && entry.Value == "{\"sentiment\":\"positive\"}");
    }

    [Fact]
    public async Task GetRecentAsync_FiltersBySessionAndRoleAndReturnsDescendingSequenceOrder()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new MessageHistory(database, CreateOptions());

        recorder.ExecuteAsyncHandler = (command, _) => command switch
        {
            "FT.SEARCH" => Task.FromResult(
                RedisResult.Create(
                    [
                        RedisResult.Create(2),
                        RedisResult.Create((RedisValue)"message-history:unit-history:tests:msg:hash:00000000000000000011"),
                        RedisResult.Create(
                            [
                                RedisResult.Create((RedisValue)"sessionId"),
                                RedisResult.Create((RedisValue)"session-1"),
                                RedisResult.Create((RedisValue)"role"),
                                RedisResult.Create((RedisValue)"assistant"),
                                RedisResult.Create((RedisValue)"content"),
                                RedisResult.Create((RedisValue)"older"),
                                RedisResult.Create((RedisValue)"metadata"),
                                RedisResult.Create((RedisValue)"{\"topic\":\"billing\"}"),
                                RedisResult.Create((RedisValue)"timestamp"),
                                RedisResult.Create((RedisValue)"1776085140000")
                            ]),
                        RedisResult.Create((RedisValue)"message-history:unit-history:tests:msg:hash:00000000000000000012"),
                        RedisResult.Create(
                            [
                                RedisResult.Create((RedisValue)"sessionId"),
                                RedisResult.Create((RedisValue)"session-1"),
                                RedisResult.Create((RedisValue)"role"),
                                RedisResult.Create((RedisValue)"assistant"),
                                RedisResult.Create((RedisValue)"content"),
                                RedisResult.Create((RedisValue)"newer"),
                                RedisResult.Create((RedisValue)"timestamp"),
                                RedisResult.Create((RedisValue)"1776085200000")
                            ])
                    ])),
            _ => Task.FromResult(RedisResult.Create((RedisValue)"OK"))
        };

        var messages = await history.GetRecentAsync("session-1", limit: 2, role: "assistant");

        Assert.Equal("FT.SEARCH", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal("@sessionId:{session\\-1} @role:{assistant}", recorder.ExecuteAsyncCalls[0].Arguments[1]);
        Assert.Equal("SORTBY", recorder.ExecuteAsyncCalls[0].Arguments[2]);
        Assert.Equal("sequence", recorder.ExecuteAsyncCalls[0].Arguments[3]);
        Assert.Equal("DESC", recorder.ExecuteAsyncCalls[0].Arguments[4]);
        Assert.Equal(["newer", "older"], messages.Select(static message => message.Content).ToArray());
        Assert.Equal([12L, 11L], messages.Select(static message => message.Sequence).ToArray());
        Assert.Equal("{\"topic\":\"billing\"}", messages[1].Metadata);
    }

    [Fact]
    public void Constructor_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => new MessageHistoryOptions(" "));
    }

    [Fact]
    public async Task GetRecentAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var history = new MessageHistory(database, CreateOptions());

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => history.GetRecentAsync("session-1", cancellationToken: cancellationTokenSource.Token));
        Assert.Empty(recorder.ExecuteAsyncCalls);
    }

    private static MessageHistoryOptions CreateOptions() =>
        new("unit-history", "tests");

    private class RecordingDatabaseProxy : DispatchProxy
    {
        public Func<string, object?[]?, Task<RedisResult>>? ExecuteAsyncHandler { get; set; }

        public long StringIncrementAsyncResult { get; set; } = 1;

        public List<RecordedCommand> ExecuteAsyncCalls { get; } = [];

        public int HashSetAsyncCallCount { get; private set; }

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
            HashSetAsyncCallCount++;
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
