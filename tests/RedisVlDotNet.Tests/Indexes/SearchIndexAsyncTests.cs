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
    public async Task MultiVectorSearchAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateMultiVectorSchema("cancel-multi-search"));
        var query = new MultiVectorQuery(
            [
                MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
            ],
            2);

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.SearchAsync(query, cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task TextSearchAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("cancel-text-search"));
        var query = new TextQuery("hello world");

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.SearchAsync(query, cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task AggregateAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("cancel-aggregate"));
        var query = new AggregationQuery(groupBy: new AggregationGroupBy(reducers: [AggregationReducer.Count("total")]));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.AggregateAsync(query, cancellationToken: cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task AggregateHybridAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateVectorSchema("cancel-aggregate-hybrid"));
        var query = AggregateHybridQuery.FromFloat32(
            Filter.Text("title").Prefix("He"),
            "embedding",
            [1f, 0f],
            2,
            groupBy: new AggregationGroupBy(reducers: [AggregationReducer.Count("total")]));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.AggregateAsync(query, cancellationToken: cancellationTokenSource.Token));
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
    public async Task UpdateJsonByKeyAsync_WithCancelledToken_DoesNotExecuteRedisCommand()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateJsonSchema("cancel-update"));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.UpdateJsonByKeyAsync(
            "movie:1",
            [new JsonPartialUpdate("$.title", "Updated")],
            cancellationTokenSource.Token));
        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task UpdateHashByKeyAsync_WithCancelledToken_DoesNotWriteToRedis()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("cancel-hash-update"));

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => index.UpdateHashByKeyAsync(
            "movie:1",
            [new HashPartialUpdate("title", "Updated")],
            cancellationTokenSource.Token));
        Assert.Equal(0, recorder.HashGetAllAsyncCallCount);
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
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
    public async Task UpdateJsonByKeyAsync_ValidatesAndExecutesEachUpdatePath()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateJsonSchema("partial-update"));

        recorder.ExecuteAsyncResponses.Enqueue(RedisResult.Create((RedisValue)"{\"id\":\"movie-1\"}"));

        var updated = await index.UpdateJsonByKeyAsync(
            " movie:1 ",
            [
                new JsonPartialUpdate(" $.title ", "Updated title"),
                new JsonPartialUpdate("$.metadata.rating", 9.5d)
            ]);

        Assert.True(updated);
        Assert.Equal(3, recorder.ExecuteAsyncCallCount);
        Assert.Equal("JSON.GET", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal("movie:1", recorder.ExecuteAsyncCalls[0].Arguments[0]);
        Assert.Equal("$", recorder.ExecuteAsyncCalls[0].Arguments[1]);
        Assert.Equal("JSON.SET", recorder.ExecuteAsyncCalls[1].Command);
        Assert.Equal("movie:1", recorder.ExecuteAsyncCalls[1].Arguments[0]);
        Assert.Equal("$.title", recorder.ExecuteAsyncCalls[1].Arguments[1]);
        Assert.Equal("\"Updated title\"", recorder.ExecuteAsyncCalls[1].Arguments[2]);
        Assert.Equal("JSON.SET", recorder.ExecuteAsyncCalls[2].Command);
        Assert.Equal("$.metadata.rating", recorder.ExecuteAsyncCalls[2].Arguments[1]);
        Assert.Equal("9.5", recorder.ExecuteAsyncCalls[2].Arguments[2]);
    }

    [Fact]
    public async Task UpdateJsonByIdAsync_ReturnsFalseWhenDocumentIsMissing()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateJsonSchema("missing-doc"));
        recorder.OnExecuteAsync = (command, _) => command == "JSON.GET"
            ? Task.FromResult<RedisResult>(null!)
            : Task.FromResult(RedisResult.Create((RedisValue)"OK"));

        var updated = await index.UpdateJsonByIdAsync(
            "movie-1",
            [new JsonPartialUpdate("$.title", "Updated title")]);

        Assert.False(updated);
        Assert.Equal(1, recorder.ExecuteAsyncCallCount);
        Assert.Equal("JSON.GET", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal($"{index.Schema.Index.Prefix}movie-1", recorder.ExecuteAsyncCalls[0].Arguments[0]);
    }

    [Fact]
    public async Task UpdateJsonByKeyAsync_RejectsInvalidUpdateRequests()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateJsonSchema("invalid-update"));

        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateJsonByKeyAsync("movie:1", Array.Empty<JsonPartialUpdate>()));
        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateJsonByKeyAsync("movie:1", [new JsonPartialUpdate("$", "invalid")]));
        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateJsonByKeyAsync("movie:1", [new JsonPartialUpdate("title", "invalid")]));
        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateJsonByKeyAsync(
            "movie:1",
            [new JsonPartialUpdate("$.title", "a"), new JsonPartialUpdate(" $.title ", "b")]));

        Assert.Equal(0, recorder.ExecuteAsyncCallCount);
    }

    [Fact]
    public async Task UpdateHashByKeyAsync_ValidatesAndExecutesSingleHashSet()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("hash-update"));
        recorder.HashGetAllResponses.Enqueue([new HashEntry("title", "Heat")]);

        var updated = await index.UpdateHashByKeyAsync(
            " movie:1 ",
            [
                new HashPartialUpdate(" title ", "Updated title"),
                new HashPartialUpdate("year", 1996),
                new HashPartialUpdate("genre", new[] { "crime", "drama" })
            ]);

        Assert.True(updated);
        Assert.Equal(1, recorder.HashGetAllAsyncCallCount);
        Assert.Equal(1, recorder.HashSetAsyncCallCount);
        Assert.Equal("movie:1", recorder.HashGetAllKeys[0].ToString());
        Assert.Equal("movie:1", recorder.HashSetCalls[0].Key.ToString());
        Assert.Equal(
            ["title", "year", "genre"],
            recorder.HashSetCalls[0].Entries.Select(static entry => entry.Name.ToString()).ToArray());
        Assert.Equal("Updated title", recorder.HashSetCalls[0].Entries[0].Value.ToString());
        Assert.Equal("1996", recorder.HashSetCalls[0].Entries[1].Value.ToString());
        Assert.Equal("[\"crime\",\"drama\"]", recorder.HashSetCalls[0].Entries[2].Value.ToString());
    }

    [Fact]
    public async Task UpdateHashByIdAsync_ReturnsFalseWhenDocumentIsMissing()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("missing-hash-doc"));

        var updated = await index.UpdateHashByIdAsync(
            "movie-1",
            [new HashPartialUpdate("title", "Updated title")]);

        Assert.False(updated);
        Assert.Equal(1, recorder.HashGetAllAsyncCallCount);
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
        Assert.Equal($"{index.Schema.Index.Prefix}movie-1", recorder.HashGetAllKeys[0].ToString());
    }

    [Fact]
    public async Task UpdateHashByKeyAsync_RejectsInvalidUpdateRequests()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("invalid-hash-update"));

        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateHashByKeyAsync("movie:1", Array.Empty<HashPartialUpdate>()));
        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateHashByKeyAsync("movie:1", [new HashPartialUpdate("", "invalid")]));
        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateHashByKeyAsync("movie:1", [new HashPartialUpdate("title", null)]));
        await Assert.ThrowsAsync<ArgumentException>(() => index.UpdateHashByKeyAsync(
            "movie:1",
            [new HashPartialUpdate("title", "a"), new HashPartialUpdate(" title ", "b")]));

        Assert.Equal(0, recorder.HashGetAllAsyncCallCount);
        Assert.Equal(0, recorder.HashSetAsyncCallCount);
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

    [Fact]
    public async Task TextSearchAsync_ExecutesFtSearchWithTextQueryArguments()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("text-search"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create((RedisValue)"movie:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Heat")
                        ])
                ]));

        var results = await index.SearchAsync(new TextQuery("heat", ["title"], offset: 1, limit: 2));

        Assert.Equal(1, results.TotalCount);
        Assert.Equal(1, recorder.ExecuteAsyncCallCount);
        Assert.Equal("FT.SEARCH", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal(
            ["hash-text-search", "heat", "RETURN", "1", "title", "LIMIT", "1", "2", "DIALECT", "2"],
            recorder.ExecuteAsyncCalls[0].Arguments.Select(static argument => argument?.ToString() ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task TextSearchAsync_TypedResults_MapReturnedDocuments()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("typed-text-search"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create((RedisValue)"movie:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Heat"),
                            RedisResult.Create((RedisValue)"year"),
                            RedisResult.Create((RedisValue)"1995"),
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"crime")
                        ])
                ]));

        var results = await index.SearchAsync<HashMovieDocument>(new TextQuery("heat", ["title", "year", "genre"]));

        var document = Assert.Single(results.Documents);
        Assert.Equal("Heat", document.Title);
        Assert.Equal(1995, document.Year);
        Assert.Equal("crime", document.Genre);
    }

    [Fact]
    public async Task AggregateAsync_ExecutesFtAggregateWithAggregationArguments()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("aggregate-movies"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"crime"),
                            RedisResult.Create((RedisValue)"movie_count"),
                            RedisResult.Create((RedisValue)"2")
                        ])
                ]));

        var result = await index.AggregateAsync(
            new AggregationQuery(
                queryString: "@genre:{crime}",
                groupBy: new AggregationGroupBy(
                    ["genre"],
                    [AggregationReducer.Count("movie_count")])));

        Assert.Equal("FT.AGGREGATE", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal(
            [
                "hash-aggregate-movies",
                "@genre:{crime}",
                "GROUPBY", "1", "@genre",
                "REDUCE", "COUNT", "0", "AS", "movie_count",
                "LIMIT", "0", "10",
                "DIALECT", "2"
            ],
            recorder.ExecuteAsyncCalls[0].Arguments.Select(static argument => argument?.ToString() ?? string.Empty).ToArray());

        Assert.Equal(1, result.TotalCount);
        var row = Assert.Single(result.Rows);
        Assert.Equal("crime", row.Values["genre"]);
        Assert.Equal("2", row.Values["movie_count"]);
    }

    [Fact]
    public async Task AggregateAsync_TypedResults_MapReturnedRows()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("typed-aggregate"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"crime"),
                            RedisResult.Create((RedisValue)"movieCount"),
                            RedisResult.Create((RedisValue)"2"),
                            RedisResult.Create((RedisValue)"avgYear"),
                            RedisResult.Create((RedisValue)"1988")
                        ])
                ]));

        var results = await index.AggregateAsync<GenreAggregateRow>(
            new AggregationQuery(
                queryString: "@genre:{crime}",
                groupBy: new AggregationGroupBy(
                    ["genre"],
                    [
                        AggregationReducer.Count("movieCount"),
                        AggregationReducer.Average("year", "avgYear")
                    ])));

        var row = Assert.Single(results.Rows);
        Assert.Equal("crime", row.Genre);
        Assert.Equal(2, row.MovieCount);
        Assert.Equal(1988d, row.AvgYear);
    }

    [Fact]
    public async Task AggregateHybridAsync_ExecutesFtAggregateWithHybridArguments()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateVectorSchema("aggregate-hybrid"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"crime"),
                            RedisResult.Create((RedisValue)"matchCount"),
                            RedisResult.Create((RedisValue)"2")
                        ])
                ]));

        var result = await index.AggregateAsync(
            AggregateHybridQuery.FromFloat32(
                Filter.Text("title").Prefix("He"),
                "embedding",
                [1f, 0f],
                2,
                loadFields: ["title"],
                groupBy: new AggregationGroupBy(
                    ["genre"],
                    [AggregationReducer.Count("matchCount")])));

        Assert.Equal("FT.AGGREGATE", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal(
            [
                "vector-aggregate-hybrid",
                "(@title:He*)=>[KNN 2 @embedding $vector AS vector_distance]",
                "PARAMS", "2", "vector", "System.Byte[]",
                "LOAD", "1", "@title",
                "GROUPBY", "1", "@genre",
                "REDUCE", "COUNT", "0", "AS", "matchCount",
                "LIMIT", "0", "10",
                "DIALECT", "2"
            ],
            recorder.ExecuteAsyncCalls[0].Arguments.Select(static argument => argument?.ToString() ?? string.Empty).ToArray());

        Assert.Equal(1, result.TotalCount);
        var row = Assert.Single(result.Rows);
        Assert.Equal("crime", row.Values["genre"]);
        Assert.Equal("2", row.Values["matchCount"]);
    }

    [Fact]
    public async Task AggregateHybridAsync_TypedResults_MapReturnedRows()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateVectorSchema("typed-aggregate-hybrid"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"crime"),
                            RedisResult.Create((RedisValue)"matchCount"),
                            RedisResult.Create((RedisValue)"2"),
                            RedisResult.Create((RedisValue)"avgDistance"),
                            RedisResult.Create((RedisValue)"0.0125")
                        ])
                ]));

        var results = await index.AggregateAsync<HybridAggregateRow>(
            AggregateHybridQuery.FromFloat32(
                Filter.Text("title").Prefix("He"),
                "embedding",
                [1f, 0f],
                2,
                groupBy: new AggregationGroupBy(
                    ["genre"],
                    [
                        AggregationReducer.Count("matchCount"),
                        AggregationReducer.Average("vector_distance", "avgDistance")
                    ])));

        var row = Assert.Single(results.Rows);
        Assert.Equal("crime", row.Genre);
        Assert.Equal(2, row.MatchCount);
        Assert.Equal(0.0125d, row.AvgDistance);
    }

    [Fact]
    public async Task MultiVectorSearchAsync_CombinesPerVectorScoresDeterministically()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateMultiVectorSchema("multi-vector"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create((RedisValue)"product:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Runner"),
                            RedisResult.Create((RedisValue)"__mv_score_0"),
                            RedisResult.Create((RedisValue)"0.05")
                        ]),
                    RedisResult.Create((RedisValue)"product:2"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Hiker"),
                            RedisResult.Create((RedisValue)"__mv_score_0"),
                            RedisResult.Create((RedisValue)"0.10")
                        ]),
                    RedisResult.Create((RedisValue)"product:3"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Boot"),
                            RedisResult.Create((RedisValue)"__mv_score_0"),
                            RedisResult.Create((RedisValue)"0.20")
                        ])
                ]));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create((RedisValue)"product:2"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Hiker"),
                            RedisResult.Create((RedisValue)"__mv_score_1"),
                            RedisResult.Create((RedisValue)"0.05")
                        ]),
                    RedisResult.Create((RedisValue)"product:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Runner"),
                            RedisResult.Create((RedisValue)"__mv_score_1"),
                            RedisResult.Create((RedisValue)"0.20")
                        ]),
                    RedisResult.Create((RedisValue)"product:3"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Boot"),
                            RedisResult.Create((RedisValue)"__mv_score_1"),
                            RedisResult.Create((RedisValue)"0.30")
                        ])
                ]));

        var results = await index.SearchAsync(
            new MultiVectorQuery(
                [
                    MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                    MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
                ],
                topK: 2,
                returnFields: ["title"],
                scoreAlias: "combined_distance"));

        Assert.Equal(2, recorder.ExecuteAsyncCallCount);
        Assert.All(recorder.ExecuteAsyncCalls, static call => Assert.Equal("FT.SEARCH", call.Command));
        Assert.Equal(3, results.TotalCount);
        Assert.Equal(["product:2", "product:1"], results.Documents.Select(static document => document.Id).ToArray());
        Assert.Equal("Hiker", results.Documents[0].Values["title"]);
        Assert.Equal("Runner", results.Documents[1].Values["title"]);
        Assert.Equal("0.084999999999999992", results.Documents[0].Values["combined_distance"]);
        Assert.Equal("0.095000000000000001", results.Documents[1].Values["combined_distance"]);
    }

    [Fact]
    public async Task MultiVectorSearchAsync_TypedResults_MapReturnedDocuments()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateMultiVectorSchema("typed-multi-vector"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create((RedisValue)"product:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Runner"),
                            RedisResult.Create((RedisValue)"__mv_score_0"),
                            RedisResult.Create((RedisValue)"0.05")
                        ])
                ]));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    RedisResult.Create((RedisValue)"product:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Runner"),
                            RedisResult.Create((RedisValue)"__mv_score_1"),
                            RedisResult.Create((RedisValue)"0.20")
                        ])
                ]));

        var results = await index.SearchAsync<MultiVectorResultDocument>(
            new MultiVectorQuery(
                [
                    MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                    MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
                ],
                topK: 1,
                returnFields: ["title"],
                scoreAlias: "combinedDistance"));

        var document = Assert.Single(results.Documents);
        Assert.Equal("Runner", document.Title);
        Assert.Equal(0.095d, document.CombinedDistance, 10);
    }

    private static SearchSchema CreateHashSchema(string token) =>
        new(
            new IndexDefinition($"hash-{token}", $"movie:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);

    private static SearchSchema CreateJsonSchema(string token) =>
        new(
            new IndexDefinition($"json-{token}", $"movie:{token}:", StorageType.Json),
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

    private static SearchSchema CreateMultiVectorSchema(string token) =>
        new(
            new IndexDefinition($"multi-vector-{token}", $"product:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "text_embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2)),
                new VectorFieldDefinition(
                    "image_embedding",
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

    private sealed record GenreAggregateRow(string Genre, int MovieCount, double AvgYear);

    private sealed record HybridAggregateRow(string Genre, int MatchCount, double AvgDistance);

    private sealed record MultiVectorResultDocument(string Title, double CombinedDistance);

    private class RecordingDatabaseProxy : DispatchProxy
    {
        public int ExecuteAsyncCallCount { get; private set; }

        public int HashGetAllAsyncCallCount { get; private set; }

        public int HashSetAsyncCallCount { get; private set; }

        public Queue<HashEntry[]> HashGetAllResponses { get; } = new();

        public int KeyDeleteAsyncCallCount { get; private set; }

        public Func<string, object[], Task<RedisResult>>? OnExecuteAsync { get; set; }

        public Func<RedisKey, HashEntry[], Task<bool>>? OnHashSetAsync { get; set; }

        public Queue<RedisResult> ExecuteAsyncResponses { get; } = new();

        public List<(string Command, string Pattern, object[] Arguments)> ExecuteAsyncCalls { get; } = [];

        public List<RedisKey> HashGetAllKeys { get; } = [];

        public List<(RedisKey Key, HashEntry[] Entries)> HashSetCalls { get; } = [];

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
                nameof(IDatabase.HashGetAllAsync) => HandleHashGetAllAsync(args),
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
            ExecuteAsyncCalls.Add((command, pattern, commandArgs));

            if (OnExecuteAsync is not null)
            {
                return OnExecuteAsync(command, commandArgs);
            }

            if (ExecuteAsyncResponses.Count > 0)
            {
                return Task.FromResult(ExecuteAsyncResponses.Dequeue());
            }

            return Task.FromResult(RedisResult.Create((RedisValue)"OK"));
        }

        private Task<HashEntry[]> HandleHashGetAllAsync(object?[]? args)
        {
            HashGetAllAsyncCallCount++;
            HashGetAllKeys.Add((RedisKey)args![0]!);

            if (HashGetAllResponses.Count > 0)
            {
                return Task.FromResult(HashGetAllResponses.Dequeue());
            }

            return Task.FromResult(Array.Empty<HashEntry>());
        }

        private Task<bool> HandleHashSetAsync(object?[]? args)
        {
            HashSetAsyncCallCount++;
            HashSetCalls.Add(((RedisKey)args![0]!, (HashEntry[])args[1]!));

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
