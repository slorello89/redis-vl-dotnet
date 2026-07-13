using System.Net;
using System.Reflection;
using RedisVL.Filters;
using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

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
    public async Task LoadHashAsync_PipelinesEveryDocumentBeforeAwaitingReplies()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("pipelined-load"));

        // Hold every HSET open. Sequential awaiting would stall after the first document; a
        // pipelined batch dispatches all of them before awaiting any reply.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        recorder.OnHashSetAsync = (_, _) => gate.Task;

        var documents = Enumerable.Range(0, 5)
            .Select(i => new HashMovieDocument(i.ToString(), $"Title {i}", 1990 + i, "crime"))
            .ToArray();

        var loadTask = index.LoadHashAsync<HashMovieDocument>(documents);

        Assert.Equal(documents.Length, recorder.HashSetAsyncCallCount);
        Assert.False(loadTask.IsCompleted);

        gate.SetResult(true);
        var keys = await loadTask;

        Assert.Equal(
            documents.Select(document => $"{index.Schema.Index.Prefix}{document.Id}").ToArray(),
            keys.ToArray());
    }

    [Fact]
    public async Task ClearAsync_EnumeratesEveryMasterAndDeletesKeysIndividually()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();

        // Two masters holding different keys plus a replica that must be
        // skipped so its mirrored keyspace is not double-counted.
        var masterA = new FakeServerState { KeyProvider = _ => [(RedisKey)"movie:1", "movie:2"] };
        var masterB = new FakeServerState { KeyProvider = _ => [(RedisKey)"movie:3"] };
        var replica = new FakeServerState { IsReplica = true, KeyProvider = _ => [(RedisKey)"movie:1"] };
        recorder.SetServers(masterA, masterB, replica);

        var schema = new SearchSchema(
            new IndexDefinition("clear-idx", "movie:", StorageType.Hash),
            [new TextFieldDefinition("title")]);
        var index = new SearchIndex(database, schema);

        var deletedCount = await index.ClearAsync();

        Assert.Equal(3, deletedCount);
        Assert.NotEmpty(masterA.KeysCalls);
        Assert.NotEmpty(masterB.KeysCalls);
        Assert.Empty(replica.KeysCalls);

        // Every delete is single-key so a batch spanning multiple hash slots
        // cannot raise CROSSSLOT on a cluster; the multi-key overload is unused.
        Assert.Empty(recorder.KeyDeleteBatches);
        Assert.Equal(3, recorder.KeyDeleteAsyncCallCount);
        Assert.Equal(
            ["movie:1", "movie:2", "movie:3"],
            recorder.DeletedKeys.Select(static key => key.ToString()).Order().ToArray());
    }

    [Fact]
    public async Task ClearAsync_EscapesGlobMetacharactersAndPassesBatchSizeAsPageSize()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var master = new FakeServerState { KeyProvider = _ => [] };
        recorder.SetServers(master);

        var schema = new SearchSchema(
            new IndexDefinition("clear-idx", "movie*[1]:", StorageType.Hash),
            [new TextFieldDefinition("title")]);
        var index = new SearchIndex(database, schema);

        await index.ClearAsync(batchSize: 37);

        var call = Assert.Single(master.KeysCalls);
        Assert.Equal(@"movie\*\[1\]:*", call.Pattern.ToString());
        Assert.Equal(37, call.PageSize);
        Assert.Equal(0, recorder.KeyDeleteAsyncCallCount);
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
    public async Task FromExistingAsync_ReconstructsSchemaFromFlatFtInfoAttributes()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncResponses.Enqueue(CreateExistingIndexInfoResultWithFlatAttributes());

        var index = await SearchIndex.FromExistingAsync(database, "reconnected-idx");

        Assert.Equal(["title", "$.genre", "year", "location", "embedding"], index.Schema.Fields.Select(static field => field.Name).ToArray());
    }

    [Fact]
    public async Task FromExistingAsync_PreservesStandaloneTrailingFlagsFromRealFtInfoLayout()
    {
        // Raw attribute-row token layout captured verbatim from FT.INFO on live
        // Redis 8.8.0. Boolean options arrive as standalone tokens with no value,
        // so naive pairwise parsing pairs SORTABLE->NOSTEM / INDEXEMPTY->INDEXMISSING
        // and silently drops the trailing flag (issue #36).
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        recorder.ExecuteAsyncResponses.Enqueue(
            CreateIndexInfoWithAttributeRows(
                "JSON",
                CreateRawAttributeRow(
                    "identifier", "$.title", "attribute", "title", "type", "TEXT",
                    "WEIGHT", "2.5", "SORTABLE", "UNF", "NOSTEM", "WITHSUFFIXTRIE", "INDEXEMPTY", "INDEXMISSING"),
                CreateRawAttributeRow(
                    "identifier", "$.genre", "attribute", "genre", "type", "TAG",
                    "SEPARATOR", ";", "CASESENSITIVE", "SORTABLE", "UNF", "WITHSUFFIXTRIE", "INDEXEMPTY", "INDEXMISSING"),
                CreateRawAttributeRow(
                    "identifier", "$.year", "attribute", "year", "type", "NUMERIC",
                    "SORTABLE", "UNF", "INDEXMISSING"),
                CreateRawAttributeRow(
                    "identifier", "$.location", "attribute", "location", "type", "GEO",
                    "SORTABLE", "UNF", "INDEXMISSING")));

        var index = await SearchIndex.FromExistingAsync(database, "reconnected-idx");

        Assert.Collection(
            index.Schema.Fields,
            field =>
            {
                var textField = Assert.IsType<TextFieldDefinition>(field);
                Assert.Equal("title", textField.Name);
                Assert.True(textField.Sortable);
                Assert.True(textField.UnNormalizedForm);
                Assert.True(textField.NoStem);
                Assert.True(textField.WithSuffixTrie);
                Assert.True(textField.IndexEmpty);
                Assert.True(textField.IndexMissing);
                Assert.Equal(2.5d, textField.Weight);
            },
            field =>
            {
                var tagField = Assert.IsType<TagFieldDefinition>(field);
                Assert.Equal("genre", tagField.Name);
                Assert.Equal(';', tagField.Separator);
                Assert.True(tagField.CaseSensitive);
                Assert.True(tagField.Sortable);
                Assert.True(tagField.WithSuffixTrie);
                Assert.True(tagField.IndexEmpty);
                Assert.True(tagField.IndexMissing);
            },
            field =>
            {
                var numericField = Assert.IsType<NumericFieldDefinition>(field);
                Assert.Equal("year", numericField.Name);
                Assert.True(numericField.Sortable);
                Assert.True(numericField.UnNormalizedForm);
                Assert.True(numericField.IndexMissing);
            },
            field =>
            {
                var geoField = Assert.IsType<GeoFieldDefinition>(field);
                Assert.Equal("location", geoField.Name);
                Assert.True(geoField.Sortable);
                Assert.True(geoField.IndexMissing);
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
    public async Task SearchBatchesAsync_TextQuery_AdvancesOffsetAndStopsAfterLastBatch()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("text-batches"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create((RedisValue)"movie:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Heat Heat")
                        ]),
                    RedisResult.Create((RedisValue)"movie:2"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Heat")
                        ])
                ]));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create((RedisValue)"movie:3"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Heatwave")
                        ])
                ]));

        var batches = new List<SearchResults>();
        await foreach (var batch in index.SearchBatchesAsync(new TextQuery("heat", ["title"], limit: 1), batchSize: 2))
        {
            batches.Add(batch);
        }

        Assert.Equal(2, recorder.ExecuteAsyncCallCount);
        Assert.Collection(
            batches,
            batch =>
            {
                Assert.Equal(3, batch.TotalCount);
                Assert.Equal(["movie:1", "movie:2"], batch.Documents.Select(static document => document.Id).ToArray());
            },
            batch =>
            {
                Assert.Equal(3, batch.TotalCount);
                Assert.Equal(["movie:3"], batch.Documents.Select(static document => document.Id).ToArray());
            });
        Assert.Equal(
            [
                ["LIMIT", "0", "2", "DIALECT", "2"],
                ["LIMIT", "2", "2", "DIALECT", "2"]
            ],
            recorder.ExecuteAsyncCalls
                .Select(static call => call.Arguments.Select(static argument => argument?.ToString() ?? string.Empty).TakeLast(5).ToArray())
                .ToArray());
    }

    [Fact]
    public async Task SearchBatchesAsync_VectorQuery_ShrinksFinalBatchToRemainingTopKWindow()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateVectorSchema("vector-batches"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create((RedisValue)"movie:1"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Heat"),
                            RedisResult.Create((RedisValue)"vector_distance"),
                            RedisResult.Create((RedisValue)"0.01")
                        ]),
                    RedisResult.Create((RedisValue)"movie:2"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Thief"),
                            RedisResult.Create((RedisValue)"vector_distance"),
                            RedisResult.Create((RedisValue)"0.02")
                        ])
                ]));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create((RedisValue)"movie:3"),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"title"),
                            RedisResult.Create((RedisValue)"Arrival"),
                            RedisResult.Create((RedisValue)"vector_distance"),
                            RedisResult.Create((RedisValue)"0.03")
                        ])
                ]));

        var batches = new List<SearchResults>();
        await foreach (var batch in index.SearchBatchesAsync(
            VectorQuery.FromFloat32("embedding", [1f, 0f], topK: 3, returnFields: ["title"]),
            batchSize: 2))
        {
            batches.Add(batch);
        }

        Assert.Equal(2, batches.Count);
        Assert.Equal(
            [
                ["LIMIT", "0", "2", "DIALECT", "2"],
                ["LIMIT", "2", "1", "DIALECT", "2"]
            ],
            recorder.ExecuteAsyncCalls
                .Select(static call => call.Arguments.Select(static argument => argument?.ToString() ?? string.Empty).TakeLast(5).ToArray())
                .ToArray());
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
    public async Task AggregateBatchesAsync_TypedResults_MapRowsAcrossPages()
    {
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("aggregate-batches"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"crime"),
                            RedisResult.Create((RedisValue)"movieCount"),
                            RedisResult.Create((RedisValue)"2")
                        ]),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"thriller"),
                            RedisResult.Create((RedisValue)"movieCount"),
                            RedisResult.Create((RedisValue)"1")
                        ])
                ]));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(3),
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"genre"),
                            RedisResult.Create((RedisValue)"science-fiction"),
                            RedisResult.Create((RedisValue)"movieCount"),
                            RedisResult.Create((RedisValue)"1")
                        ])
                ]));

        var batches = new List<AggregationResults<GenreAggregateCountRow>>();
        await foreach (var batch in index.AggregateBatchesAsync<GenreAggregateCountRow>(
            new AggregationQuery(
                groupBy: new AggregationGroupBy(["genre"], [AggregationReducer.Count("movieCount")])),
            batchSize: 2))
        {
            batches.Add(batch);
        }

        Assert.Equal(2, batches.Count);
        Assert.Equal(["crime", "thriller"], batches[0].Rows.Select(static row => row.Genre).ToArray());
        Assert.Equal(["science-fiction"], batches[1].Rows.Select(static row => row.Genre).ToArray());
        Assert.Equal(
            [
                ["LIMIT", "0", "2", "DIALECT", "2"],
                ["LIMIT", "2", "2", "DIALECT", "2"]
            ],
            recorder.ExecuteAsyncCalls
                .Select(static call => call.Arguments.Select(static argument => argument?.ToString() ?? string.Empty).TakeLast(5).ToArray())
                .ToArray());
    }

    [Fact]
    public async Task AggregateBatchesAsync_NonGroupByPipeline_PagesAllRowsDespiteUnreliableCount()
    {
        // Regression test for issue #34: for LOAD/APPLY-only (non-GROUPBY) pipelines Redis
        // returns 1 as the leading reply element (surfaced as AggregationResults.TotalCount).
        // The pager must not treat that as a total row count, otherwise it stops after the first
        // batch and silently drops the remaining rows.
        var (database, recorder) = RecordingDatabaseProxy.CreatePair();
        var index = new SearchIndex(database, CreateHashSchema("aggregate-nongroup-batches"));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    NonGroupByRow("alpha"),
                    NonGroupByRow("bravo")
                ]));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    NonGroupByRow("charlie"),
                    NonGroupByRow("delta")
                ]));
        recorder.ExecuteAsyncResponses.Enqueue(
            RedisResult.Create(
                [
                    RedisResult.Create(1),
                    NonGroupByRow("echo")
                ]));

        var titles = new List<string>();
        await foreach (var batch in index.AggregateBatchesAsync(
            new AggregationQuery(loadFields: ["title"]),
            batchSize: 2))
        {
            titles.AddRange(batch.Rows.Select(static row => row.Values["title"].ToString()));
        }

        Assert.Equal(["alpha", "bravo", "charlie", "delta", "echo"], titles);
        Assert.Equal(3, recorder.ExecuteAsyncCallCount);
        Assert.Equal(
            [
                ["LIMIT", "0", "2", "DIALECT", "2"],
                ["LIMIT", "2", "2", "DIALECT", "2"],
                ["LIMIT", "4", "2", "DIALECT", "2"]
            ],
            recorder.ExecuteAsyncCalls
                .Select(static call => call.Arguments.Select(static argument => argument?.ToString() ?? string.Empty).TakeLast(5).ToArray())
                .ToArray());

        static RedisResult NonGroupByRow(string title) =>
            RedisResult.Create(
                [
                    RedisResult.Create((RedisValue)"title"),
                    RedisResult.Create((RedisValue)title)
                ]);
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
                    [AggregationReducer.Count("matchCount")]),
                runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 80)));

        Assert.Equal("FT.AGGREGATE", recorder.ExecuteAsyncCalls[0].Command);
        Assert.Equal(
            [
                "vector-aggregate-hybrid",
                "(@title:He*)=>[KNN 2 @embedding $vector EF_RUNTIME $ef_runtime AS vector_distance]",
                "PARAMS", "4", "vector", "System.Byte[]", "ef_runtime", "80",
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
                topK: 3,
                returnFields: ["title"],
                scoreAlias: "combined_distance",
                pagination: new QueryPagination(offset: 1, limit: 2)));

        Assert.Equal(2, recorder.ExecuteAsyncCallCount);
        Assert.All(recorder.ExecuteAsyncCalls, static call => Assert.Equal("FT.SEARCH", call.Command));
        Assert.Equal(3, results.TotalCount);
        Assert.Equal(["product:1", "product:3"], results.Documents.Select(static document => document.Id).ToArray());
        Assert.Equal("Runner", results.Documents[0].Values["title"]);
        Assert.Equal("Boot", results.Documents[1].Values["title"]);
        // Compare the fused score numerically with tolerance rather than against an exact
        // double->string round-trip: 0.7*0.05 + 0.3*0.20 = 0.095 and 0.7*0.20 + 0.3*0.30 = 0.23.
        // The literal string form ("0.095000000000000001") is an artifact of IEEE-754 formatting and
        // would break on any harmless change to how the fused distance is rendered.
        Assert.Equal(0.095, (double)results.Documents[0].Values["combined_distance"], precision: 6);
        Assert.Equal(0.23, (double)results.Documents[1].Values["combined_distance"], precision: 6);
        Assert.All(
            recorder.ExecuteAsyncCalls,
            static call => Assert.Equal(
                ["LIMIT", "0", "3", "DIALECT", "2"],
                call.Arguments.Select(static argument => argument?.ToString() ?? string.Empty).TakeLast(5).ToArray()));
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

    private static SearchSchema CreateVectorSchema(string token) =>
        new(
            new IndexDefinition($"vector-{token}", $"vector:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2,
                        m: 16,
                        efConstruction: 200))
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

    private static RedisResult CreateExistingIndexInfoResultWithFlatAttributes() =>
        RedisResult.Create(
            CreateRedisPairs(
                ("index_name", RedisResult.Create((RedisValue)"reconnected-idx")),
                (
                    "index_options",
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"MAXTEXTFIELDS"),
                            RedisResult.Create((RedisValue)"TEMPORARY"),
                            RedisResult.Create((RedisValue)"300")
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
                                ])
                        ])),
                (
                    "attributes",
                    RedisResult.Create(
                        CreateRedisPairs(
                            ("identifier", RedisResult.Create((RedisValue)"$.title")),
                            ("attribute", RedisResult.Create((RedisValue)"title")),
                            ("type", RedisResult.Create((RedisValue)"TEXT")),
                            ("identifier", RedisResult.Create((RedisValue)"$.genre")),
                            ("attribute", RedisResult.Create((RedisValue)"movieGenre")),
                            ("type", RedisResult.Create((RedisValue)"TAG")),
                            ("SEPARATOR", RedisResult.Create((RedisValue)";")),
                            ("identifier", RedisResult.Create((RedisValue)"$.year")),
                            ("attribute", RedisResult.Create((RedisValue)"year")),
                            ("type", RedisResult.Create((RedisValue)"NUMERIC")),
                            ("identifier", RedisResult.Create((RedisValue)"$.location")),
                            ("attribute", RedisResult.Create((RedisValue)"location")),
                            ("type", RedisResult.Create((RedisValue)"GEO")),
                            ("identifier", RedisResult.Create((RedisValue)"$.embedding")),
                            ("attribute", RedisResult.Create((RedisValue)"embedding")),
                            ("type", RedisResult.Create((RedisValue)"VECTOR")),
                            ("algorithm", RedisResult.Create((RedisValue)"HNSW")),
                            ("data_type", RedisResult.Create((RedisValue)"FLOAT32")),
                            ("dim", RedisResult.Create((RedisValue)"3")),
                            ("distance_metric", RedisResult.Create((RedisValue)"COSINE")))))));

    // Boolean field options (SORTABLE, NOSTEM, INDEXMISSING, ...) are emitted by
    // FT.INFO as standalone flag tokens with no value, e.g.
    // "... WEIGHT 2.5 SORTABLE UNF NOSTEM WITHSUFFIXTRIE INDEXEMPTY INDEXMISSING".
    // Only WEIGHT/PHONETIC/SEPARATOR/vector-parameter entries carry a value token.
    private static readonly HashSet<string> StandaloneAttributeFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "SORTABLE",
        "UNF",
        "NOSTEM",
        "NOINDEX",
        "CASESENSITIVE",
        "WITHSUFFIXTRIE",
        "INDEXEMPTY",
        "INDEXMISSING",
    };

    private static RedisResult CreateFieldAttributeRow(params (string Key, string Value)[] entries) =>
        RedisResult.Create(entries.SelectMany(static entry =>
            StandaloneAttributeFlags.Contains(entry.Key)
                ? new[] { RedisResult.Create((RedisValue)entry.Key) }
                : new[]
                {
                    RedisResult.Create((RedisValue)entry.Key),
                    RedisResult.Create((RedisValue)entry.Value)
                }).ToArray());

    private static RedisResult CreateRawAttributeRow(params string[] tokens) =>
        RedisResult.Create(tokens.Select(static token => RedisResult.Create((RedisValue)token)).ToArray());

    private static RedisResult CreateIndexInfoWithAttributeRows(string keyType, params RedisResult[] attributeRows) =>
        RedisResult.Create(
            CreateRedisPairs(
                ("index_name", RedisResult.Create((RedisValue)"reconnected-idx")),
                (
                    "index_definition",
                    RedisResult.Create(
                        [
                            RedisResult.Create((RedisValue)"key_type"),
                            RedisResult.Create((RedisValue)keyType),
                            RedisResult.Create((RedisValue)"prefixes"),
                            RedisResult.Create([RedisResult.Create((RedisValue)"doc:")])
                        ])),
                ("attributes", RedisResult.Create(attributeRows))));

    private static RedisResult[] CreateRedisPairs(params (string Key, RedisResult Value)[] entries) =>
        entries.SelectMany(static entry => new[] { RedisResult.Create((RedisValue)entry.Key), entry.Value }).ToArray();

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record GenreAggregateRow(string Genre, int MovieCount, double AvgYear);

    private sealed record HybridAggregateRow(string Genre, int MatchCount, double AvgDistance);

    private sealed record MultiVectorResultDocument(string Title, double CombinedDistance);

    private sealed record GenreAggregateCountRow(string Genre, int MovieCount);

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

        public List<RedisKey> DeletedKeys { get; } = [];

        private IConnectionMultiplexer? _multiplexer;

        public static (IDatabase Database, RecordingDatabaseProxy Recorder) CreatePair()
        {
            var database = DispatchProxy.Create<IDatabase, RecordingDatabaseProxy>();
            var recorder = (RecordingDatabaseProxy)(object)database;
            return (database, recorder);
        }

        public void SetServers(params FakeServerState[] servers) =>
            _multiplexer = FakeConnectionMultiplexerProxy.Create(servers);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            return targetMethod.Name switch
            {
                nameof(IDatabase.ExecuteAsync) => HandleExecuteAsync(args),
                nameof(IDatabase.HashGetAllAsync) => HandleHashGetAllAsync(args),
                nameof(IDatabase.HashSetAsync) => HandleHashSetAsync(args),
                nameof(IDatabase.KeyDeleteAsync) => HandleKeyDeleteAsync(args),
                "get_Multiplexer" => _multiplexer
                    ?? throw new InvalidOperationException("Call SetServers before accessing Multiplexer."),
                "get_Database" => 0,
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

        private object HandleKeyDeleteAsync(object?[]? args)
        {
            KeyDeleteAsyncCallCount++;

            // The single-key overload is slot-safe on a cluster; the multi-key
            // overload is only recorded so a regression back to it is visible.
            if (args![0] is RedisKey[] batch)
            {
                KeyDeleteBatches.Add(batch);
                return Task.FromResult((long)batch.Length);
            }

            DeletedKeys.Add((RedisKey)args[0]!);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeServerState
    {
        public bool IsConnected { get; init; } = true;

        public bool IsReplica { get; init; }

        public ServerType ServerType { get; init; } = ServerType.Standalone;

        public Func<RedisValue, IEnumerable<RedisKey>> KeyProvider { get; init; } = static _ => [];

        public List<(int Database, RedisValue Pattern, int PageSize)> KeysCalls { get; } = [];
    }

    private class FakeConnectionMultiplexerProxy : DispatchProxy
    {
        private EndPoint[] _endpoints = [];
        private Dictionary<EndPoint, IServer> _servers = new();

        public static IConnectionMultiplexer Create(IReadOnlyList<FakeServerState> servers)
        {
            var multiplexer = DispatchProxy.Create<IConnectionMultiplexer, FakeConnectionMultiplexerProxy>();
            var proxy = (FakeConnectionMultiplexerProxy)(object)multiplexer;

            var endpoints = new EndPoint[servers.Count];
            var map = new Dictionary<EndPoint, IServer>();
            for (var i = 0; i < servers.Count; i++)
            {
                var endpoint = new DnsEndPoint("fake-node", i);
                endpoints[i] = endpoint;
                map[endpoint] = FakeServerProxy.Create(servers[i]);
            }

            proxy._endpoints = endpoints;
            proxy._servers = map;
            return multiplexer;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            return targetMethod.Name switch
            {
                nameof(IConnectionMultiplexer.GetEndPoints) => _endpoints,
                nameof(IConnectionMultiplexer.GetServer) => _servers[(EndPoint)args![0]!],
                _ => throw new NotSupportedException($"IConnectionMultiplexer.{targetMethod.Name} is not configured for this test proxy.")
            };
        }
    }

    private class FakeServerProxy : DispatchProxy
    {
        private FakeServerState _state = new();

        public static IServer Create(FakeServerState state)
        {
            var server = DispatchProxy.Create<IServer, FakeServerProxy>();
            ((FakeServerProxy)(object)server)._state = state;
            return server;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);

            return targetMethod.Name switch
            {
                "get_IsConnected" => _state.IsConnected,
                "get_IsReplica" => _state.IsReplica,
                "get_ServerType" => _state.ServerType,
                nameof(IServer.KeysAsync) => HandleKeysAsync(args),
                _ => throw new NotSupportedException($"IServer.{targetMethod.Name} is not configured for this test proxy.")
            };
        }

        private IAsyncEnumerable<RedisKey> HandleKeysAsync(object?[]? args)
        {
            var database = (int)args![0]!;
            var pattern = (RedisValue)args[1]!;
            var pageSize = (int)args[2]!;
            _state.KeysCalls.Add((database, pattern, pageSize));
            return ToAsyncEnumerable(_state.KeyProvider(pattern));
        }

        private static async IAsyncEnumerable<RedisKey> ToAsyncEnumerable(IEnumerable<RedisKey> keys)
        {
            await Task.CompletedTask;
            foreach (var key in keys)
            {
                yield return key;
            }
        }
    }
}
