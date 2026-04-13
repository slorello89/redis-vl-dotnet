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
    public async Task ClearAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("cancel-clear"));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.ClearAsync(cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
        Assert.Equal(0, recorder.KeyDeleteAsyncCallCount);
    }

    [Fact]
    public async Task ListAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => SearchIndex.ListAsync(database, cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task FromExistingAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => SearchIndex.FromExistingAsync(database, "existing-idx", cancellationTokenSource.Token));
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

    [Fact]
    public async Task ClearAsync_ScansAndDeletesAllMatchingPrefixKeys()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var schema = new SearchSchema(
            new IndexDefinition("clear-idx", ["movie:", "archive:"], StorageType.Hash),
            [new TextFieldDefinition("title")]);
        var index = new SearchIndex(database, schema);

        recorder.ExecuteAsyncResponses.Enqueue(CreateScanResult(1, ["movie:1", "movie:2"]));
        recorder.ExecuteAsyncResponses.Enqueue(CreateScanResult(0, ["movie:3"]));
        recorder.ExecuteAsyncResponses.Enqueue(CreateScanResult(0, ["archive:1"]));

        var deletedCount = await index.ClearAsync(batchSize: 2);

        Assert.Equal(4, deletedCount);
        Assert.Equal(3, recorder.ExecuteAsyncCallCount);
        Assert.Equal(3, recorder.KeyDeleteAsyncCallCount);
        Assert.Equal(
            ["movie:*", "movie:*", "archive:*"],
            recorder.ExecuteAsyncCalls.Select(static call => call.Pattern).ToArray());
        Assert.Equal(
            ["movie:1,movie:2", "movie:3", "archive:1"],
            recorder.KeyDeleteBatches
                .Select(static batch => string.Join(',', batch.Select(static key => key.ToString())))
                .ToArray());
    }

    [Fact]
    public async Task FromExistingAsync_ReconstructsSchemaFromFtInfo()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncResponses.Enqueue(CreateExistingIndexInfoResult());

        var index = await SearchIndex.FromExistingAsync(database, "reconnected-idx");

        Assert.Equal("reconnected-idx", index.Schema.Index.Name);
        Assert.Equal(StorageType.Json, index.Schema.Index.StorageType);
        Assert.Equal(["movie:", "archive:"], index.Schema.Index.Prefixes);
        Assert.Equal('|', index.Schema.Index.KeySeparator);
        Assert.Equal(["the", "a"], index.Schema.Index.Stopwords);
        Assert.True(index.Schema.Index.MaxTextFields);
        Assert.Equal(300, index.Schema.Index.TemporarySeconds);
        Assert.True(index.Schema.Index.NoOffsets);
        Assert.True(index.Schema.Index.NoHighlight);
        Assert.True(index.Schema.Index.NoFields);
        Assert.True(index.Schema.Index.NoFrequencies);
        Assert.True(index.Schema.Index.SkipInitialScan);

        Assert.Collection(
            index.Schema.Fields,
            field =>
            {
                var textField = Assert.IsType<TextFieldDefinition>(field);
                Assert.Equal("title", textField.Name);
                Assert.Null(textField.Alias);
                Assert.True(textField.Sortable);
                Assert.True(textField.NoStem);
                Assert.True(textField.PhoneticMatch);
                Assert.Equal(2.5d, textField.Weight);
                Assert.True(textField.WithSuffixTrie);
                Assert.True(textField.IndexMissing);
                Assert.True(textField.IndexEmpty);
                Assert.True(textField.NoIndex);
                Assert.True(textField.UnNormalizedForm);
            },
            field =>
            {
                var tagField = Assert.IsType<TagFieldDefinition>(field);
                Assert.Equal("$.genre", tagField.Name);
                Assert.Equal("movieGenre", tagField.Alias);
                Assert.True(tagField.Sortable);
                Assert.Equal(';', tagField.Separator);
                Assert.True(tagField.CaseSensitive);
                Assert.True(tagField.WithSuffixTrie);
                Assert.True(tagField.IndexMissing);
                Assert.True(tagField.IndexEmpty);
                Assert.True(tagField.NoIndex);
            },
            field =>
            {
                var numericField = Assert.IsType<NumericFieldDefinition>(field);
                Assert.Equal("year", numericField.Name);
                Assert.True(numericField.Sortable);
                Assert.True(numericField.IndexMissing);
                Assert.True(numericField.NoIndex);
                Assert.True(numericField.UnNormalizedForm);
            },
            field =>
            {
                var geoField = Assert.IsType<GeoFieldDefinition>(field);
                Assert.Equal("location", geoField.Name);
                Assert.True(geoField.Sortable);
                Assert.True(geoField.IndexMissing);
                Assert.True(geoField.NoIndex);
            },
            field =>
            {
                var vectorField = Assert.IsType<VectorFieldDefinition>(field);
                Assert.Equal("embedding", vectorField.Name);
                Assert.Null(vectorField.Alias);
                Assert.True(vectorField.IndexMissing);
                Assert.Equal(VectorAlgorithm.Hnsw, vectorField.Attributes.Algorithm);
                Assert.Equal(VectorDataType.Float32, vectorField.Attributes.DataType);
                Assert.Equal(VectorDistanceMetric.Cosine, vectorField.Attributes.DistanceMetric);
                Assert.Equal(3, vectorField.Attributes.Dimensions);
                Assert.Equal(100, vectorField.Attributes.InitialCapacity);
                Assert.Equal(16, vectorField.Attributes.M);
                Assert.Equal(200, vectorField.Attributes.EfConstruction);
                Assert.Equal(10, vectorField.Attributes.EfRuntime);
            });
    }

    private static SearchSchema CreateHashSchema(string token) =>
        new(
            new IndexDefinition($"hash-{token}", $"movie:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);

    private static RedisResult CreateScanResult(long cursor, params string[] keys) =>
        RedisResult.Create(
            [
                RedisResult.Create((RedisValue)cursor.ToString()),
                RedisResult.Create(keys.Select(static key => RedisResult.Create((RedisValue)key)).ToArray())
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

    private static RedisResult CreateExistingIndexInfoResult() =>
        RedisResult.Create(
            CreateRedisPairs(
                ("index_name", RedisResult.Create((RedisValue)"reconnected-idx")),
                (
                    "index_options",
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"MAXTEXTFIELDS"),
                            RedisResult.Create((RedisValue)"TEMPORARY"),
                            RedisResult.Create((RedisValue)"300"),
                            RedisResult.Create((RedisValue)"NOOFFSETS"),
                            RedisResult.Create((RedisValue)"NOHL"),
                            RedisResult.Create((RedisValue)"NOFIELDS"),
                            RedisResult.Create((RedisValue)"NOFREQS"),
                            RedisResult.Create((RedisValue)"SKIPINITIALSCAN")
                        ])),
                (
                    "index_definition",
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"key_type"),
                            RedisResult.Create((RedisValue)"JSON"),
                            RedisResult.Create((RedisValue)"prefixes"),
                            RedisResult.Create(
                                [
                                    RedisResult.Create((RedisValue)"movie:"),
                                    RedisResult.Create((RedisValue)"archive:")
                                ]),
                            RedisResult.Create((RedisValue)"separator"),
                            RedisResult.Create((RedisValue)"|")
                        ])),
                (
                    "attributes",
                    RedisResult.Create(
                        [
                            CreateFieldAttributeRow(
                                ("identifier", "$.title"),
                                ("attribute", "title"),
                                ("type", "TEXT"),
                                ("WEIGHT", "2.5"),
                                ("SORTABLE", "true"),
                                ("UNF", "true"),
                                ("NOSTEM", "true"),
                                ("PHONETIC", "dm:en"),
                                ("WITHSUFFIXTRIE", "true"),
                                ("INDEXEMPTY", "true"),
                                ("INDEXMISSING", "true"),
                                ("NOINDEX", "true")),
                            CreateFieldAttributeRow(
                                ("identifier", "$.genre"),
                                ("attribute", "movieGenre"),
                                ("type", "TAG"),
                                ("SEPARATOR", ";"),
                                ("SORTABLE", "true"),
                                ("CASESENSITIVE", "true"),
                                ("WITHSUFFIXTRIE", "true"),
                                ("INDEXEMPTY", "true"),
                                ("INDEXMISSING", "true"),
                                ("NOINDEX", "true")),
                            CreateFieldAttributeRow(
                                ("identifier", "$.year"),
                                ("attribute", "year"),
                                ("type", "NUMERIC"),
                                ("SORTABLE", "true"),
                                ("UNF", "true"),
                                ("INDEXMISSING", "true"),
                                ("NOINDEX", "true")),
                            CreateFieldAttributeRow(
                                ("identifier", "$.location"),
                                ("attribute", "location"),
                                ("type", "GEO"),
                                ("SORTABLE", "true"),
                                ("INDEXMISSING", "true"),
                                ("NOINDEX", "true")),
                            CreateFieldAttributeRow(
                                ("identifier", "$.embedding"),
                                ("attribute", "embedding"),
                                ("type", "VECTOR"),
                                ("algorithm", "HNSW"),
                                ("data_type", "FLOAT32"),
                                ("dim", "3"),
                                ("distance_metric", "COSINE"),
                                ("initial_cap", "100"),
                                ("m", "16"),
                                ("ef_construction", "200"),
                                ("ef_runtime", "10"),
                                ("INDEXMISSING", "true"))
                        ])),
                (
                    "stopwords_list",
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"the"),
                            RedisResult.Create((RedisValue)"a")
                        ]))));

    private static RedisResult CreateFieldAttributeRow(params (string Key, string Value)[] entries) =>
        RedisResult.Create(entries.SelectMany(static entry => new[]
        {
            RedisResult.Create((RedisValue)entry.Key),
            RedisResult.Create((RedisValue)entry.Value)
        }).ToArray());

    private static RedisResult[] CreateRedisPairs(params (string Key, RedisResult Value)[] entries) =>
        entries.SelectMany(static entry => new[] { RedisResult.Create((RedisValue)entry.Key), entry.Value }).ToArray();

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private class RecordingDatabaseProxy : DispatchProxy
    {
        public int ExecuteAsyncCallCount { get; private set; }

        public int HashGetAllAsyncCallCount { get; private set; }

        public int HashSetAsyncCallCount { get; private set; }

        public int KeyDeleteAsyncCallCount { get; private set; }

        public Func<RedisKey, HashEntry[], Task<bool>>? OnHashSetAsync { get; set; }

        public Queue<RedisResult> ExecuteAsyncResponses { get; } = new();

        public List<(string Command, string Pattern)> ExecuteAsyncCalls { get; } = [];

        public List<RedisKey[]> KeyDeleteBatches { get; } = [];

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
                nameof(IDatabase.KeyDeleteAsync) => HandleKeyDeleteAsync(args),
                nameof(IDatabase.Multiplexer) => throw new NotSupportedException(),
                nameof(IDatabase.Database) => 0,
                _ => throw new NotSupportedException($"Method '{targetMethod.Name}' is not configured for this test proxy.")
            };
        }

        private Task<RedisResult> HandleExecuteAsync(object?[]? args)
        {
            ExecuteAsyncCallCount++;
            var command = (string)args![0]!;
            var commandArgs = (object[]?)args[1] ?? [];
            var pattern = command.Equals("SCAN", StringComparison.Ordinal)
                ? commandArgs[2]?.ToString() ?? string.Empty
                : string.Empty;
            ExecuteAsyncCalls.Add((command, pattern));

            if (ExecuteAsyncResponses.Count > 0)
            {
                return Task.FromResult(ExecuteAsyncResponses.Dequeue());
            }

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

        private Task<long> HandleKeyDeleteAsync(object?[]? args)
        {
            KeyDeleteAsyncCallCount++;
            var keys = (RedisKey[])args![0]!;
            KeyDeleteBatches.Add(keys);
            return Task.FromResult((long)keys.Length);
        }
    }
}
