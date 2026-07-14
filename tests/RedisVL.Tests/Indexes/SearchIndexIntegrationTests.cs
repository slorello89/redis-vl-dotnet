using RedisVL.Indexes;
using RedisVL.Filters;
using RedisVL.Queries;
using RedisVL.Schema;
using StackExchange.Redis;

namespace RedisVL.Tests.Indexes;

public sealed class SearchIndexIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task CreatesInspectsAndDropsIndex()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"movies-idx-{token}", $"movie:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title", sortable: true),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            var created = await index.CreateAsync();
            var skipped = await index.CreateAsync(new CreateIndexOptions(skipIfExists: true));
            var overwritten = await index.CreateAsync(new CreateIndexOptions(overwrite: true));
            var exists = await index.ExistsAsync();
            var info = await index.InfoAsync();

            Assert.True(created);
            Assert.False(skipped);
            Assert.True(overwritten);
            Assert.True(exists);
            Assert.Equal(schema.Index.Name, info.Name);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync();
            }
        }

        Assert.False(await index.ExistsAsync());
    }

    [RedisSearchIntegrationFact]
    public async Task ListsCreatedIndexes()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var firstIndex = new SearchIndex(
            database,
            new SearchSchema(
                new IndexDefinition($"movies-list-a-{token}", $"movie:list:a:{token}:", StorageType.Hash),
                [
                    new TextFieldDefinition("title"),
                    new TagFieldDefinition("genre")
                ]));
        var secondIndex = new SearchIndex(
            database,
            new SearchSchema(
                new IndexDefinition($"movies-list-b-{token}", $"movie:list:b:{token}:", StorageType.Hash),
                [
                    new TextFieldDefinition("title"),
                    new TagFieldDefinition("genre")
                ]));

        try
        {
            await firstIndex.CreateAsync();
            await secondIndex.CreateAsync();

            var indexes = await SearchIndex.ListAsync(database);
            var indexNames = indexes.Select(static item => item.Name).ToHashSet(StringComparer.Ordinal);

            Assert.Contains(firstIndex.Schema.Index.Name, indexNames);
            Assert.Contains(secondIndex.Schema.Index.Name, indexNames);
        }
        finally
        {
            if (await firstIndex.ExistsAsync())
            {
                await firstIndex.DropAsync();
            }

            if (await secondIndex.ExistsAsync())
            {
                await secondIndex.DropAsync();
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ReconnectsToExistingIndexAndReusesSchemaForQueries()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition(
                $"movies-reconnect-idx-{token}",
                [$"movie:{token}:", $"archive:{token}:"],
                StorageType.Hash,
                keySeparator: '|',
                stopwords: ["the", "a"],
                maxTextFields: true,
                noOffsets: true),
            [
                new TextFieldDefinition("title", sortable: true),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var originalIndex = new SearchIndex(database, schema);

        try
        {
            await originalIndex.CreateAsync();
            await originalIndex.LoadHashAsync(
                [
                    new HashMovieDocument("movie-1", "Heat", 1995, "crime"),
                    new HashMovieDocument("movie-2", "Arrival", 2016, "science-fiction")
                ]);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(originalIndex, 2);

            var reconnectedIndex = await SearchIndex.FromExistingAsync(database, schema.Index.Name);
            var results = await reconnectedIndex.SearchAsync<HashMovieDocument>(
                new FilterQuery(Filter.Tag("genre").Eq("crime"), ["title", "year", "genre"]));
            var fetched = await reconnectedIndex.FetchHashByIdAsync<HashMovieDocument>("movie-1");

            Assert.Equal(schema.Index.Name, reconnectedIndex.Schema.Index.Name);
            Assert.Equal(schema.Index.Prefixes, reconnectedIndex.Schema.Index.Prefixes);
            Assert.Equal(':', reconnectedIndex.Schema.Index.KeySeparator);
            Assert.Equal(schema.Index.StorageType, reconnectedIndex.Schema.Index.StorageType);
            Assert.True(reconnectedIndex.Schema.Index.MaxTextFields);
            Assert.True(reconnectedIndex.Schema.Index.NoOffsets);
            Assert.Equal(["title", "year", "genre"], reconnectedIndex.Schema.Fields.Select(static field => field.Name).ToArray());
            Assert.Single(results.Documents);
            Assert.Equal("Heat", results.Documents[0].Title);
            Assert.Equal("Heat", fetched!.Title);
        }
        finally
        {
            if (await originalIndex.ExistsAsync())
            {
                await originalIndex.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task CreatesIndexWithMultiplePrefixes()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var primaryPrefix = $"movie:{token}:";
        var secondaryPrefix = $"archive:{token}:";
        var schema = new SearchSchema(
            new IndexDefinition($"movies-multi-prefix-idx-{token}", [primaryPrefix, secondaryPrefix], StorageType.Hash),
            [
                new TextFieldDefinition("title", sortable: true),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await database.HashSetAsync($"{primaryPrefix}movie-1", [new HashEntry("title", "Heat"), new HashEntry("genre", "crime")]);
            await database.HashSetAsync($"{secondaryPrefix}movie-2", [new HashEntry("title", "Arrival"), new HashEntry("genre", "science-fiction")]);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 2);

            var crimeCount = await index.CountAsync(new CountQuery(Filter.Tag("genre").Eq("crime")));
            var results = await index.SearchAsync(new FilterQuery(Filter.Tag("genre").Eq("science-fiction"), ["title", "genre"]));

            Assert.Equal(1, crimeCount);
            Assert.Single(results.Documents);
            Assert.Equal($"{secondaryPrefix}movie-2", results.Documents[0].Id);
            Assert.Equal("Arrival", results.Documents[0].Values["title"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task CreatesIndexWithKeySeparatorAndStopwords()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition(
                $"movies-stopwords-idx-{token}",
                $"movie:{token}:",
                StorageType.Hash,
                keySeparator: '|',
                stopwords: ["the", "a", "an"]),
            [
                new TextFieldDefinition("title", sortable: true),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();

            var info = await index.InfoAsync();

            Assert.True(info.TryGetValue("index_definition", out var definitionValue));
            Assert.True(info.TryGetValue("stopwords_list", out var stopwordsValue));

            var definition = ToFlatStringDictionary(definitionValue);
            var stopwords = ((RedisResult[])stopwordsValue!).Select(static entry => entry.ToString()!).ToArray();

            Assert.Equal(["a", "an", "the"], stopwords.OrderBy(static value => value, StringComparer.Ordinal).ToArray());
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task CreatesIndexWithAdvancedFieldAndIndexOptions()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition(
                $"movies-advanced-idx-{token}",
                $"movie:{token}:",
                StorageType.Hash,
                maxTextFields: true,
                temporarySeconds: 300,
                noOffsets: true,
                noHighlight: true,
                noFrequencies: true,
                skipInitialScan: true),
            [
                new TextFieldDefinition(
                    "title",
                    sortable: true,
                    weight: 2.5,
                    withSuffixTrie: true,
                    indexMissing: true,
                    indexEmpty: true,
                    unNormalizedForm: true),
                new TagFieldDefinition(
                    "genre",
                    sortable: true,
                    withSuffixTrie: true,
                    indexEmpty: true,
                    noIndex: true),
                new NumericFieldDefinition("rating", sortable: true, indexMissing: true, unNormalizedForm: true),
                new GeoFieldDefinition("location", sortable: true, noIndex: true),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        3,
                        initialCapacity: 100,
                        m: 16,
                        efConstruction: 200,
                        efRuntime: 10),
                    indexMissing: true)
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();

            var info = await index.InfoAsync();

            Assert.True(info.TryGetValue("index_options", out var indexOptionsValue));
            Assert.True(info.TryGetValue("attributes", out var attributesValue));

            var indexOptions = FlattenRedisResult(indexOptionsValue).ToArray();
            var attributeRows = (RedisResult[])attributesValue!;
            var flattenedAttributes = attributeRows.SelectMany(FlattenRedisResult).ToArray();

            Assert.Contains("NOOFFSETS", indexOptions);
            Assert.Contains("NOHL", indexOptions);
            Assert.Contains("NOFREQS", indexOptions);
            Assert.Contains("MAXTEXTFIELDS", indexOptions);

            Assert.Contains("WITHSUFFIXTRIE", flattenedAttributes);
            Assert.Contains("INDEXEMPTY", flattenedAttributes);
            Assert.Contains("INDEXMISSING", flattenedAttributes);
            Assert.Contains("NOINDEX", flattenedAttributes);
            Assert.Contains("UNF", flattenedAttributes);
            Assert.Contains("WEIGHT", flattenedAttributes);
            Assert.Contains("2.5", flattenedAttributes);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task CreatesIndexWithNoFieldsOption()
    {
        // NOFIELDS is validated in its own index because newer RediSearch rejects
        // combining MAXTEXTFIELDS with NOFIELDS ("MAXTEXTFIELDS cannot be used with NOFIELDS").
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition(
                $"movies-nofields-idx-{token}",
                $"movie:nofields:{token}:",
                StorageType.Hash,
                noFields: true),
            [
                new TextFieldDefinition("title")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();

            var info = await index.InfoAsync();

            Assert.True(info.TryGetValue("index_options", out var indexOptionsValue));

            var indexOptions = FlattenRedisResult(indexOptionsValue).ToArray();

            Assert.Contains("NOFIELDS", indexOptions);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesAsyncIndexAndTypedQueryFlowWithCancellationToken()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"async-flow-idx-{token}", $"async-flow:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await index.CreateAsync(cancellationToken: cancellationTokenSource.Token);
            await index.LoadHashAsync(
                [
                    new HashMovieDocument("movie-1", "Heat", 1995, "crime"),
                    new HashMovieDocument("movie-2", "Arrival", 2016, "science-fiction")
                ],
                cancellationToken: cancellationTokenSource.Token);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 2, cancellationTokenSource.Token);

            var results = await index.SearchAsync<HashMovieDocument>(
                new FilterQuery(
                    Filter.Numeric("year").GreaterThan(1990),
                    ["title", "year", "genre"]),
                cancellationToken: cancellationTokenSource.Token);
            var count = await index.CountAsync(
                new CountQuery(Filter.Tag("genre").Eq("crime")),
                cancellationTokenSource.Token);
            var fetched = await index.FetchHashByIdAsync<HashMovieDocument>("movie-1", cancellationTokenSource.Token);
            var deleted = await index.DeleteHashByIdAsync("movie-1", cancellationTokenSource.Token);

            Assert.Equal(2, results.Documents.Count);
            Assert.Contains(results.Documents, static document => document.Title == "Heat");
            Assert.Equal(1, count);
            Assert.Equal("Heat", fetched!.Title);
            Assert.True(deleted);
        }
        finally
        {
            if (await index.ExistsAsync(cancellationTokenSource.Token))
            {
                await index.DropAsync(deleteDocuments: true, cancellationTokenSource.Token);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task LoadsFetchesAndDeletesJsonDocuments()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"json-docs-idx-{token}", $"jsondoc:{token}:", StorageType.Json),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();

            var loadedKey = await index.LoadJsonAsync(new JsonMovieDocument("movie-1", "Heat", 1995, "crime"));
            var batchKeys = await index.LoadJsonAsync(
                [
                    new JsonMovieEnvelope("movie-2", "Alien", 1979, "sci-fi"),
                    new JsonMovieEnvelope("movie-3", "Arrival", 2016, "sci-fi")
                ],
                idSelector: static document => document.ExternalId);
            var customKey = await index.LoadJsonAsync(
                new JsonMovieEnvelope("unused", "Thief", 1981, "crime"),
                key: $"{schema.Index.Prefix}custom");

            var fetchedById = await index.FetchJsonByIdAsync<JsonMovieDocument>("movie-1");
            var fetchedByKey = await index.FetchJsonByKeyAsync<JsonMovieEnvelope>(batchKeys[0]);
            var fetchedCustom = await index.FetchJsonByKeyAsync<JsonMovieEnvelope>(customKey);
            var deletedById = await index.DeleteJsonByIdAsync("movie-1");
            var deletedByKey = await index.DeleteJsonByKeyAsync(customKey);
            var missingAfterDelete = await index.FetchJsonByIdAsync<JsonMovieDocument>("movie-1");

            Assert.Equal($"{schema.Index.Prefix}movie-1", loadedKey);
            Assert.Equal(
                [$"{schema.Index.Prefix}movie-2", $"{schema.Index.Prefix}movie-3"],
                batchKeys);
            Assert.Equal("Heat", fetchedById!.Title);
            Assert.Equal("Alien", fetchedByKey!.Title);
            Assert.Equal("Thief", fetchedCustom!.Title);
            Assert.True(deletedById);
            Assert.True(deletedByKey);
            Assert.Null(missingAfterDelete);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ClearsJsonDocumentsWithoutDroppingIndex()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"json-clear-idx-{token}", $"jsonclear:{token}:", StorageType.Json),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await index.LoadJsonAsync(
                [
                    new JsonMovieDocument("movie-1", "Heat", 1995, "crime"),
                    new JsonMovieDocument("movie-2", "Arrival", 2016, "science-fiction")
                ]);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 2);

            var deletedCount = await index.ClearAsync();

            await RedisSearchTestEnvironment.WaitForAsync(async () => await index.CountAsync(new CountQuery()) == 0);

            Assert.Equal(2, deletedCount);
            Assert.True(await index.ExistsAsync());
            Assert.Equal(schema.Index.Name, (await index.InfoAsync()).Name);
            Assert.Null(await index.FetchJsonByIdAsync<JsonMovieDocument>("movie-1"));
            Assert.Null(await index.FetchJsonByIdAsync<JsonMovieDocument>("movie-2"));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task PartiallyUpdatesJsonDocumentsByIdAndByKey()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"json-update-idx-{token}", $"jsonupdate:{token}:", StorageType.Json),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();

            var key = await index.LoadJsonAsync(new JsonMovieWithMetadata(
                "movie-1",
                "Heat",
                1995,
                "crime",
                new JsonMovieMetadata("Michael Mann", 8.0d)));

            var updatedById = await index.UpdateJsonByIdAsync(
                "movie-1",
                [
                    new JsonPartialUpdate("$.title", "Heat: Director's Cut"),
                    new JsonPartialUpdate("$.metadata.rating", 9.25d)
                ]);
            var updatedByKey = await index.UpdateJsonByKeyAsync(
                key,
                [new JsonPartialUpdate("$.metadata.director", "M. Mann")]);
            var updated = await index.FetchJsonByIdAsync<JsonMovieWithMetadata>("movie-1");

            Assert.True(updatedById);
            Assert.True(updatedByKey);
            Assert.Equal("Heat: Director's Cut", updated!.Title);
            Assert.Equal(9.25d, updated.Metadata.Rating);
            Assert.Equal("M. Mann", updated.Metadata.Director);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task LoadsFetchesAndDeletesHashDocuments()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"hash-docs-idx-{token}", $"hashdoc:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();

            var loadedKey = await index.LoadHashAsync(new HashMovieDocument("movie-1", "Heat", 1995, "crime"));
            var batchKeys = await index.LoadHashAsync(
                [
                    new HashMovieEnvelope("movie-2", "Alien", 1979, "sci-fi"),
                    new HashMovieEnvelope("movie-3", "Arrival", 2016, "sci-fi")
                ],
                idSelector: static document => document.ExternalId);
            var customKey = await index.LoadHashAsync(
                new HashMovieEnvelope("unused", "Thief", 1981, "crime"),
                key: $"{schema.Index.Prefix}custom");

            var fetchedById = await index.FetchHashByIdAsync<HashMovieDocument>("movie-1");
            var fetchedByKey = await index.FetchHashByKeyAsync<HashMovieEnvelope>(batchKeys[0]);
            var fetchedCustom = await index.FetchHashByKeyAsync<HashMovieEnvelope>(customKey);
            var deletedById = await index.DeleteHashByIdAsync("movie-1");
            var deletedByKey = await index.DeleteHashByKeyAsync(customKey);
            var missingAfterDelete = await index.FetchHashByIdAsync<HashMovieDocument>("movie-1");

            Assert.Equal($"{schema.Index.Prefix}movie-1", loadedKey);
            Assert.Equal(
                [$"{schema.Index.Prefix}movie-2", $"{schema.Index.Prefix}movie-3"],
                batchKeys);
            Assert.Equal("Heat", fetchedById!.Title);
            Assert.Equal("Alien", fetchedByKey!.Title);
            Assert.Equal("Thief", fetchedCustom!.Title);
            Assert.True(deletedById);
            Assert.True(deletedByKey);
            Assert.Null(missingAfterDelete);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task PartiallyUpdatesHashDocumentsByIdAndByKey()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"hash-update-idx-{token}", $"hashupdate:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();

            var key = await index.LoadHashAsync(new HashMovieDocument("movie-1", "Heat", 1995, "crime"));
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 1);

            var updatedById = await index.UpdateHashByIdAsync(
                "movie-1",
                [
                    new HashPartialUpdate("title", "Heat: Director's Cut"),
                    new HashPartialUpdate("year", 1996)
                ]);
            var updatedByKey = await index.UpdateHashByKeyAsync(
                key,
                [new HashPartialUpdate("genre", "neo-noir")]);
            var updated = await index.FetchHashByIdAsync<HashMovieDocument>("movie-1");
            var results = await index.SearchAsync<HashMovieDocument>(
                new FilterQuery(Filter.Tag("genre").Eq("neo-noir"), ["title", "year", "genre"]));

            Assert.True(updatedById);
            Assert.True(updatedByKey);
            Assert.Equal("Heat: Director's Cut", updated!.Title);
            Assert.Equal(1996, updated.Year);
            Assert.Equal("neo-noir", updated.Genre);
            Assert.Single(results.Documents);
            Assert.Equal("Heat: Director's Cut", results.Documents[0].Title);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ClearsHashDocumentsWithoutDroppingIndex()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"hash-clear-idx-{token}", $"hashclear:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await index.LoadHashAsync(
                [
                    new HashMovieDocument("movie-1", "Heat", 1995, "crime"),
                    new HashMovieDocument("movie-2", "Arrival", 2016, "science-fiction")
                ]);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 2);

            var deletedCount = await index.ClearAsync();

            await RedisSearchTestEnvironment.WaitForAsync(async () => await index.CountAsync(new CountQuery()) == 0);

            Assert.Equal(2, deletedCount);
            Assert.True(await index.ExistsAsync());
            Assert.Equal(schema.Index.Name, (await index.InfoAsync()).Name);
            Assert.Null(await index.FetchHashByIdAsync<HashMovieDocument>("movie-1"));
            Assert.Null(await index.FetchHashByIdAsync<HashMovieDocument>("movie-2"));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesFilterAndCountQueriesAcrossSupportedFieldTypes()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"filter-idx-{token}", $"filter:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new NumericFieldDefinition("year"),
                new TextFieldDefinition("title"),
                new GeoFieldDefinition("location")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.FilterMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.FilterMovies.Count);

            var tagResults = await index.SearchAsync(new FilterQuery(
                Filter.Tag("genre").Eq("crime"),
                ["title", "genre"],
                limit: 10));
            var numericResults = await index.SearchAsync(new FilterQuery(
                Filter.Numeric("year").GreaterThan(1990),
                ["title", "year"],
                limit: 10));
            var textResults = await index.SearchAsync(new FilterQuery(
                Filter.Text("title").Prefix("Arr"),
                ["title"],
                limit: 10));
            var geoResults = await index.SearchAsync(new FilterQuery(
                Filter.Geo("location").WithinRadius(-118.2437, 34.0522, 50, RedisVL.Filters.GeoUnit.Miles),
                ["title"],
                limit: 10));
            var crimeCount = await index.CountAsync(new CountQuery(Filter.Tag("genre").Eq("crime")));

            Assert.Equal(2, tagResults.TotalCount);
            Assert.Equal(
                [$"{schema.Index.Prefix}1", $"{schema.Index.Prefix}2"],
                tagResults.Documents
                    .Select(static document => document.Id)
                    .OrderBy(static id => id, StringComparer.Ordinal)
                    .ToArray());
            Assert.Equal(2, numericResults.TotalCount);
            Assert.Equal(
                [$"{schema.Index.Prefix}1", $"{schema.Index.Prefix}3"],
                numericResults.Documents
                    .Select(static document => document.Id)
                    .OrderBy(static id => id, StringComparer.Ordinal)
                    .ToArray());
            Assert.Single(textResults.Documents);
            Assert.Equal($"{schema.Index.Prefix}3", textResults.Documents[0].Id);
            Assert.Single(geoResults.Documents);
            Assert.Equal($"{schema.Index.Prefix}1", geoResults.Documents[0].Id);
            Assert.Equal(2, crimeCount);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesTextQueriesWithDeterministicRankingAndProjectedFields()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"text-query-idx-{token}", $"text-query:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title", weight: 3.0),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.TextQueryMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.TextQueryMovies.Count);

            var results = await index.SearchAsync(new TextQuery("heat", ["title", "year"], limit: 2));
            var documentsByTitle = results.Documents.ToDictionary(
                static document => document.Values["title"].ToString(),
                StringComparer.Ordinal);

            Assert.Equal(2, results.TotalCount);
            Assert.Equal(2, results.Documents.Count);
            Assert.Equal(["Heat", "Heat Heat"], documentsByTitle.Keys.OrderBy(static title => title, StringComparer.Ordinal).ToArray());
            Assert.Equal($"{schema.Index.Prefix}1", documentsByTitle["Heat Heat"].Id);
            Assert.Equal("1995", documentsByTitle["Heat Heat"].Values["year"]);
            Assert.False(documentsByTitle["Heat Heat"].TryGetValue("genre", out _));
            Assert.Equal($"{schema.Index.Prefix}2", documentsByTitle["Heat"].Id);
            Assert.Equal("1981", documentsByTitle["Heat"].Values["year"]);
            Assert.False(documentsByTitle["Heat"].TryGetValue("genre", out _));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesTypedTextQueriesWithProjectedResults()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"typed-text-query-idx-{token}", $"typed-text-query:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title", weight: 3.0),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.TextQueryMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.TextQueryMovies.Count);

            var results = await index.SearchAsync<HashMovieDocument>(
                new TextQuery("heat", ["title", "year", "genre"], limit: 2));

            Assert.Equal(2, results.TotalCount);
            Assert.Equal(
                ["Heat Heat", "Heat"],
                results.Documents.Select(static document => document.Title).ToArray());
            Assert.Equal([1995, 1981], results.Documents.Select(static document => document.Year).ToArray());
            Assert.All(results.Documents, static document => Assert.Equal("crime", document.Genre));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesTextQueriesAcrossPages()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"text-query-pages-idx-{token}", $"text-query-pages:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title", weight: 3.0),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.TextQueryMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.TextQueryMovies.Count);

            var firstPage = await index.SearchAsync(new TextQuery("heat", ["title"], pagination: new QueryPagination(limit: 1)));
            var secondPage = await index.SearchAsync(new TextQuery("heat", ["title"], pagination: new QueryPagination(offset: 1, limit: 1)));
            var returnedTitles = firstPage.Documents
                .Concat(secondPage.Documents)
                .Select(static document => document.Values["title"].ToString())
                .OrderBy(static title => title, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(2, firstPage.TotalCount);
            Assert.Equal(2, secondPage.TotalCount);
            Assert.Single(firstPage.Documents);
            Assert.Single(secondPage.Documents);
            Assert.Equal(["Heat", "Heat Heat"], returnedTitles);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task IteratesTextQueryBatchesDeterministically()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"text-query-batches-idx-{token}", $"text-query-batches:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title", weight: 3.0),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.TextQueryMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.TextQueryMovies.Count);

            var batches = new List<SearchResults>();
            await foreach (var batch in index.SearchBatchesAsync(
                new TextQuery("heat", ["title"], pagination: new QueryPagination(limit: 1)),
                batchSize: 1))
            {
                batches.Add(batch);
            }

            Assert.Equal(2, batches.Count);
            Assert.All(batches, static batch => Assert.Equal(2, batch.TotalCount));
            Assert.Equal("Heat Heat", Assert.Single(batches[0].Documents).Values["title"]);
            Assert.Equal("Heat", Assert.Single(batches[1].Documents).Values["title"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesAggregationQueriesWithGroupedRowsAndTypedReducers()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"aggregate-idx-{token}", $"aggregate:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.AggregationMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.AggregationMovies.Count);

            var query = new AggregationQuery(
                groupBy: new AggregationGroupBy(
                    ["genre"],
                    [
                        AggregationReducer.Count("movieCount"),
                        AggregationReducer.Average("year", "averageYear")
                    ]),
                sortBy: new AggregationSortBy(
                    [
                        new AggregationSortField("movieCount", descending: true),
                        new AggregationSortField("averageYear", descending: true)
                    ]),
                limit: 10);

            var rawResults = await index.AggregateAsync(query);
            var typedResults = await index.AggregateAsync<GenreAggregationRow>(query);

            // TotalCount (the FT.AGGREGATE leading count) is documented as unreliable and varies by
            // protocol/version, so assert on the reliable returned-row count instead.
            Assert.Equal(2, rawResults.Rows.Count);
            Assert.Equal(["crime", "science-fiction"], rawResults.Rows.Select(static row => row.Values["genre"].ToString()).ToArray());
            Assert.Equal(["2", "1"], rawResults.Rows.Select(static row => row.Values["movieCount"].ToString()).ToArray());
            Assert.Equal(["1988", "2016"], rawResults.Rows.Select(static row => row.Values["averageYear"].ToString()).ToArray());

            Assert.Equal(2, typedResults.Rows.Count);
            Assert.Collection(
                typedResults.Rows,
                row =>
                {
                    Assert.Equal("crime", row.Genre);
                    Assert.Equal(2, row.MovieCount);
                    Assert.Equal(1988d, row.AverageYear);
                },
                row =>
                {
                    Assert.Equal("science-fiction", row.Genre);
                    Assert.Equal(1, row.MovieCount);
                    Assert.Equal(2016d, row.AverageYear);
                });
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesAggregationQueriesAcrossPages()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"aggregate-pages-idx-{token}", $"aggregate-pages:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.AggregationMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.AggregationMovies.Count);

            var query = new AggregationQuery(
                groupBy: new AggregationGroupBy(
                    ["genre"],
                    [
                        AggregationReducer.Count("movieCount"),
                        AggregationReducer.Average("year", "averageYear")
                    ]),
                sortBy: new AggregationSortBy(
                    [
                        new AggregationSortField("movieCount", descending: true),
                        new AggregationSortField("averageYear", descending: true)
                    ]),
                pagination: new QueryPagination(offset: 1, limit: 1));

            var results = await index.AggregateAsync(query);

            // Paging (offset 1, limit 1) must return the second group as its own page. TotalCount
            // (the FT.AGGREGATE leading count) is documented as unreliable, so it is not asserted.
            var row = Assert.Single(results.Rows);
            Assert.Equal("science-fiction", row.Values["genre"]);
            Assert.Equal("1", row.Values["movieCount"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task AggregateBatchesAsyncPagesAllRowsForNonGroupedPipeline()
    {
        // Regression test for issue #34: for non-GROUPBY (LOAD-only) pipelines Redis returns 1 as
        // the leading FT.AGGREGATE reply element, so paging must not rely on TotalCount to stop.
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"aggregate-nongroup-idx-{token}", $"aggregate-nongroup:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new NumericFieldDefinition("year"),
                new TagFieldDefinition("genre")
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.NonGroupedAggregationMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.NonGroupedAggregationMovies.Count);

            // No SORTBY: a sorted non-GROUPBY pipeline makes Redis return the true row count as the
            // leading reply element, which would mask the bug. LOAD-only keeps the leading element
            // pinned at 1 so this exercises the truncation path from issue #34.
            var query = new AggregationQuery(loadFields: ["title"]);

            var titles = new List<string>();
            var batchCount = 0;
            await foreach (var batch in index.AggregateBatchesAsync(query, batchSize: 2))
            {
                batchCount++;
                titles.AddRange(batch.Rows.Select(static row => row.Values["title"].ToString()));
            }

            Assert.Equal(SearchIndexSeedData.NonGroupedAggregationMovies.Count, titles.Count);
            Assert.Equal(["Arrival", "Collateral", "Dune", "Heat", "Thief"], titles.OrderBy(static title => title, StringComparer.Ordinal).ToArray());
            Assert.True(batchCount >= 3, $"Expected at least 3 batches when paging 5 rows two at a time but observed {batchCount}.");
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesAggregateHybridQueriesWithDeterministicGrouping()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"aggregate-hybrid-idx-{token}", $"aggregate-hybrid:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.AggregateHybridMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.AggregateHybridMovies.Count);

            var query = AggregateHybridQuery.FromFloat32(
                Filter.Text("title").Prefix("He") | Filter.Text("title").Prefix("Ar"),
                "embedding",
                [1f, 0f],
                3,
                groupBy: new AggregationGroupBy(
                    ["genre"],
                    [
                        AggregationReducer.Count("matchCount"),
                        AggregationReducer.Average("vector_distance", "avgDistance")
                    ]),
                sortBy: new AggregationSortBy(
                    [
                        new AggregationSortField("matchCount", descending: true),
                        new AggregationSortField("avgDistance")
                    ]),
                limit: 10);

            var rawResults = await index.AggregateAsync(query);
            var typedResults = await index.AggregateAsync<HybridAggregationRow>(query);

            // TotalCount (the FT.AGGREGATE leading count) is documented as unreliable and varies by
            // protocol/version, so assert on the reliable returned-row count instead.
            Assert.Equal(2, rawResults.Rows.Count);
            Assert.Equal(["crime", "science-fiction"], rawResults.Rows.Select(static row => row.Values["genre"].ToString()).ToArray());
            Assert.Equal(["2", "1"], rawResults.Rows.Select(static row => row.Values["matchCount"].ToString()).ToArray());

            Assert.Equal(2, typedResults.Rows.Count);
            Assert.Collection(
                typedResults.Rows,
                row =>
                {
                    Assert.Equal("crime", row.Genre);
                    Assert.Equal(2, row.MatchCount);
                    Assert.True(row.AvgDistance >= 0d);
                },
                row =>
                {
                    Assert.Equal("science-fiction", row.Genre);
                    Assert.Equal(1, row.MatchCount);
                    Assert.True(row.AvgDistance >= 0d);
                });
            Assert.True(typedResults.Rows[0].AvgDistance < typedResults.Rows[1].AvgDistance);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesVectorQueriesWithDeterministicRanking()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-idx-{token}", $"vector:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            var query = VectorQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                2,
                Filter.Tag("genre").Eq("crime"),
                ["title"],
                scoreAlias: "distance");

            var results = await index.SearchAsync(query);

            Assert.Equal(2, results.Documents.Count);
            Assert.Equal($"{schema.Index.Prefix}1", results.Documents[0].Id);
            Assert.Equal($"{schema.Index.Prefix}2", results.Documents[1].Id);
            Assert.Equal("Heat", results.Documents[0].Values["title"]);
            Assert.Equal("Thief", results.Documents[1].Values["title"]);
            Assert.True(double.Parse(results.Documents[0].Values["distance"].ToString(), System.Globalization.CultureInfo.InvariantCulture) <
                        double.Parse(results.Documents[1].Values["distance"].ToString(), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task TypedVectorSearchWithoutReturnFieldsMapsStoredFields()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-typed-idx-{token}", $"vector-typed:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            // No return fields specified: the query used to emit `RETURN 1 vector_distance`, so mapping
            // a POCO with non-nullable properties threw. It must now return all stored fields so the
            // obvious typed happy path just works.
            var results = await index.SearchAsync<VectorMovieRow>(
                VectorQuery.FromFloat32("embedding", [1f, 0f], 2));

            Assert.Equal(2, results.Documents.Count);
            Assert.All(results.Documents, static row => Assert.False(string.IsNullOrEmpty(row.Title)));
            Assert.Contains(results.Documents, static row => row.Title == "Heat");
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task TypedVectorRangeSearchWithoutReturnFieldsMapsStoredFields()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-range-typed-idx-{token}", $"vrange-typed:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            // No return fields specified: the range query used to emit `RETURN 1 vector_distance`, so mapping
            // a POCO with non-nullable properties threw. It must now return all stored fields so the obvious
            // typed happy path just works. Threshold 0.3 from [1, 0] admits "Heat" (0.0) and "Thief" (~0.03).
            var results = await index.SearchAsync<VectorMovieRow>(
                VectorRangeQuery.FromFloat32("embedding", [1f, 0f], 0.3));

            Assert.Equal(2, results.Documents.Count);
            Assert.All(results.Documents, static row => Assert.False(string.IsNullOrEmpty(row.Title)));
            Assert.Contains(results.Documents, static row => row.Title == "Heat");
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesVectorQueriesWithCompoundFilters()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-compound-idx-{token}", $"vector-compound:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            // AND: crime genre AND title matching "heat" -> only "Heat" (doc 1).
            var andResults = await index.SearchAsync(VectorQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                3,
                Filter.Tag("genre").Eq("crime") & Filter.Text("title").Match("heat"),
                ["title", "genre"],
                scoreAlias: "distance"));

            Assert.Equal($"{schema.Index.Prefix}1", Assert.Single(andResults.Documents).Id);

            // OR: science-fiction genre OR title matching "thief" -> "Thief" (doc 2) and "Arrival" (doc 3),
            // ordered by ascending distance from [1, 0].
            var orResults = await index.SearchAsync(VectorQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                3,
                Filter.Tag("genre").Eq("science-fiction") | Filter.Text("title").Match("thief"),
                ["title", "genre"],
                scoreAlias: "distance"));

            Assert.Equal(2, orResults.Documents.Count);
            Assert.Equal(
                [$"{schema.Index.Prefix}2", $"{schema.Index.Prefix}3"],
                orResults.Documents.Select(document => document.Id).ToArray());

            // NOT: everything that is not crime -> only "Arrival" (doc 3).
            var notResults = await index.SearchAsync(VectorQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                3,
                Filter.Not(Filter.Tag("genre").Eq("crime")),
                ["title", "genre"],
                scoreAlias: "distance"));

            var notDocument = Assert.Single(notResults.Documents);
            Assert.Equal($"{schema.Index.Prefix}3", notDocument.Id);
            Assert.Equal("Arrival", notDocument.Values["title"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesVectorQueriesAcrossPages()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-pages-idx-{token}", $"vector-pages:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            var firstPage = await index.SearchAsync(VectorQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                2,
                Filter.Tag("genre").Eq("crime"),
                ["title"],
                scoreAlias: "distance",
                pagination: new QueryPagination(limit: 1)));
            var secondPage = await index.SearchAsync(VectorQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                2,
                Filter.Tag("genre").Eq("crime"),
                ["title"],
                scoreAlias: "distance",
                pagination: new QueryPagination(offset: 1, limit: 1)));

            Assert.Equal(2, firstPage.TotalCount);
            Assert.Equal(2, secondPage.TotalCount);
            Assert.Equal("Heat", Assert.Single(firstPage.Documents).Values["title"]);
            Assert.Equal("Thief", Assert.Single(secondPage.Documents).Values["title"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesHnswVectorQueriesWithRuntimeEfRuntimeParameter()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-hnsw-idx-{token}", $"vector-hnsw:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
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
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            var query = VectorQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                2,
                Filter.Tag("genre").Eq("crime"),
                ["title"],
                scoreAlias: "distance",
                runtimeOptions: new VectorKnnRuntimeOptions(efRuntime: 150));

            var results = await index.SearchAsync(query);

            Assert.Equal(2, results.Documents.Count);
            Assert.Equal("Heat", results.Documents[0].Values["title"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesHybridQueriesWithDeterministicRanking()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"hybrid-idx-{token}", $"hybrid:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.HybridMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.HybridMovies.Count);

            var query = HybridQuery.FromFloat32(
                Filter.Text("title").Prefix("He"),
                "embedding",
                [1f, 0f],
                2,
                Filter.Tag("genre").Eq("crime"),
                ["title"],
                scoreAlias: "distance");

            var results = await index.SearchAsync(query);

            Assert.Equal(2, results.Documents.Count);
            Assert.Equal($"{schema.Index.Prefix}1", results.Documents[0].Id);
            Assert.Equal($"{schema.Index.Prefix}2", results.Documents[1].Id);
            Assert.Equal("Heat", results.Documents[0].Values["title"]);
            Assert.Equal("Heatwave", results.Documents[1].Values["title"]);
            Assert.True(double.Parse(results.Documents[0].Values["distance"].ToString(), System.Globalization.CultureInfo.InvariantCulture) <
                        double.Parse(results.Documents[1].Values["distance"].ToString(), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesVectorRangeQueriesWithThresholdOrdering()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-range-idx-{token}", $"vrange:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            var query = VectorRangeQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                0.3,
                Filter.Tag("genre").Eq("crime"),
                ["title"],
                scoreAlias: "distance",
                limit: 10);

            var results = await index.SearchAsync(query);

            Assert.Equal(2, results.Documents.Count);
            Assert.Equal($"{schema.Index.Prefix}1", results.Documents[0].Id);
            Assert.Equal($"{schema.Index.Prefix}2", results.Documents[1].Id);
            Assert.Equal("Heat", results.Documents[0].Values["title"]);
            Assert.Equal("Thief", results.Documents[1].Values["title"]);
            Assert.True(double.Parse(results.Documents[0].Values["distance"].ToString(), System.Globalization.CultureInfo.InvariantCulture) <
                        double.Parse(results.Documents[1].Values["distance"].ToString(), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task VectorRangeQueryWithTopLevelOrFilterExcludesOutOfRangeDocuments()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-range-or-idx-{token}", $"vrange-or:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
                new TextFieldDefinition("title"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            // Arrival (science-fiction, embedding [0, 1]) has cosine distance 1.0 to the query
            // vector [1, 0] — far outside the 0.1 radius. It matches the left branch of the OR
            // filter. Before the parenthesization fix the query parsed as
            // `science-fiction | (crime AND range)`, so Arrival leaked in via the unconstrained
            // left branch with no distance constraint.
            const double distanceThreshold = 0.1;
            var query = VectorRangeQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                distanceThreshold,
                Filter.Tag("genre").Eq("science-fiction") | Filter.Tag("genre").Eq("crime"),
                ["title", "genre"],
                scoreAlias: "distance",
                limit: 10);

            var results = await index.SearchAsync(query);

            Assert.DoesNotContain(
                results.Documents,
                document => document.Values.TryGetValue("title", out var title) && title == "Arrival");

            // Every returned document must carry a distance within the requested radius.
            foreach (var document in results.Documents)
            {
                Assert.True(document.Values.TryGetValue("distance", out var distanceValue) && !distanceValue.IsNull,
                    $"Document '{document.Id}' was returned without a distance score.");
                var distance = double.Parse(distanceValue.ToString(), System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(distance <= distanceThreshold,
                    $"Document '{document.Id}' has distance {distance}, exceeding threshold {distanceThreshold}.");
            }

            Assert.Equal(2, results.Documents.Count);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesHnswVectorRangeQueriesWithRuntimeEpsilonParameter()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"vector-range-hnsw-idx-{token}", $"vrange-hnsw:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("genre"),
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
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.VectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.VectorMovies.Count);

            var query = VectorRangeQuery.FromFloat32(
                "embedding",
                [1f, 0f],
                0.3,
                Filter.Tag("genre").Eq("crime"),
                ["title"],
                scoreAlias: "distance",
                limit: 10,
                runtimeOptions: new VectorRangeRuntimeOptions(epsilon: 0.05));

            var results = await index.SearchAsync(query);

            Assert.Equal(2, results.Documents.Count);
            Assert.Equal("Heat", results.Documents[0].Values["title"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesMultiVectorQueriesWithDeterministicRanking()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"multi-vector-idx-{token}", $"multi-vector:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("category"),
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
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.MultiVectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.MultiVectorMovies.Count);

            var query = new MultiVectorQuery(
                [
                    MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                    MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
                ],
                topK: 3,
                filter: Filter.Tag("category").Eq("footwear"),
                returnFields: ["title"],
                scoreAlias: "combined_distance");

            var results = await index.SearchAsync(query);

            Assert.Equal(3, results.Documents.Count);
            Assert.Equal($"{schema.Index.Prefix}1", results.Documents[0].Id);
            Assert.Equal($"{schema.Index.Prefix}2", results.Documents[1].Id);
            Assert.Equal($"{schema.Index.Prefix}3", results.Documents[2].Id);
            Assert.Equal("Runner", results.Documents[0].Values["title"]);
            Assert.Equal("Hiker", results.Documents[1].Values["title"]);
            Assert.Equal("Boot", results.Documents[2].Values["title"]);

            var scores = results.Documents
                .Select(document => double.Parse(document.Values["combined_distance"].ToString(), System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();

            Assert.True(scores[0] < scores[1]);
            Assert.True(scores[1] < scores[2]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ExecutesMultiVectorQueriesAcrossPages()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"multi-vector-pages-idx-{token}", $"multi-vector-pages:{token}:", StorageType.Hash),
            [
                new TagFieldDefinition("category"),
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
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.MultiVectorMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.MultiVectorMovies.Count);

            var firstPage = await index.SearchAsync(new MultiVectorQuery(
                [
                    MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                    MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
                ],
                topK: 3,
                filter: Filter.Tag("category").Eq("footwear"),
                returnFields: ["title"],
                scoreAlias: "combined_distance",
                pagination: new QueryPagination(limit: 1)));
            var secondPage = await index.SearchAsync(new MultiVectorQuery(
                [
                    MultiVectorInput.FromFloat32("text_embedding", [1f, 0f], weight: 0.7),
                    MultiVectorInput.FromFloat32("image_embedding", [0f, 1f], weight: 0.3)
                ],
                topK: 3,
                filter: Filter.Tag("category").Eq("footwear"),
                returnFields: ["title"],
                scoreAlias: "combined_distance",
                pagination: new QueryPagination(offset: 1, limit: 1)));

            Assert.Equal(3, firstPage.TotalCount);
            Assert.Equal(3, secondPage.TotalCount);
            Assert.Equal("Runner", Assert.Single(firstPage.Documents).Values["title"]);
            Assert.Equal("Hiker", Assert.Single(secondPage.Documents).Values["title"]);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task TagLikeMatchesSingleCharacterWildcard()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"tag-like-idx-{token}", $"tag-like:{token}:", StorageType.Hash),
            [new TagFieldDefinition("sku")]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await database.HashSetAsync($"{schema.Index.Prefix}1", [new HashEntry("sku", "ab1")]);
            await database.HashSetAsync($"{schema.Index.Prefix}2", [new HashEntry("sku", "ab2")]);
            await database.HashSetAsync($"{schema.Index.Prefix}3", [new HashEntry("sku", "abcd")]);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 3);

            // `?` matches exactly one character only through the w'...' form Like now emits: the two
            // three-character SKUs match and the four-character one does not. The old plain-{...}
            // rendering treated `?` literally and silently matched nothing.
            var results = await index.SearchAsync(new FilterQuery(Filter.Tag("sku").Like("ab?")));

            Assert.Equal(2, results.Documents.Count);
            Assert.DoesNotContain(results.Documents, static document => document.Values["sku"] == "abcd");
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    private sealed record JsonMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record JsonMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

    private sealed record JsonMovieWithMetadata(string Id, string Title, int Year, string Genre, JsonMovieMetadata Metadata);

    private sealed record JsonMovieMetadata(string Director, double Rating);

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record HashMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

    private sealed record GenreAggregationRow(string Genre, int MovieCount, double AverageYear);

    private sealed record HybridAggregationRow(string Genre, int MatchCount, double AvgDistance);

    private sealed record HybridSearchRow(string Id, string Title);

    private sealed record VectorMovieRow(string Id, string Title, string Genre);

    private static IReadOnlyDictionary<string, string> ToFlatStringDictionary(RedisResult result)
    {
        var entries = (RedisResult[])result!;
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < entries.Length; index += 2)
        {
            dictionary[entries[index].ToString()!] = entries[index + 1].ToString()!;
        }

        return dictionary;
    }

    private static IEnumerable<string> FlattenRedisResult(RedisResult result)
    {
        if (result.IsNull)
        {
            yield break;
        }

        if (result.Resp2Type == ResultType.Array)
        {
            var entries = (RedisResult[]?)result ?? [];
            foreach (var entry in entries)
            {
                foreach (var value in FlattenRedisResult(entry))
                {
                    yield return value;
                }
            }

            yield break;
        }

        yield return result.ToString()!;
    }

    [RedisNativeHybridSearchIntegrationFact]
    public async Task ExecutesNativeHybridSearchQueriesWithLinearFusion()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"native-hybrid-idx-{token}", $"native-hybrid:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.HybridMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.HybridMovies.Count);

            // "He*" matches "Heat" and "Heatwave"; [1,0] is an exact match for "Heat" on the vector branch.
            var query = HybridSearchQuery.FromFloat32(
                Filter.Text("title").Prefix("He"),
                "embedding",
                [1f, 0f],
                3,
                combination: new LinearHybridCombination(0.7, 0.3),
                returnFields: ["title"]);

            var results = await index.SearchAsync(query);
            var typed = await index.SearchAsync<HybridSearchRow>(query);

            // "He*" matches Heat (:1) and Heatwave (:2) on the text branch; the KNN branch can also
            // surface Arrival (:3). FT.HYBRID min-max normalizes each branch independently before
            // fusing, so the text branch maps the Heat/Heatwave BM25 scores onto {0, 1} no matter how
            // close they are. With text weighted 0.7 that makes the *fused ranking* depend on Redis's
            // internal BM25 tie-breaking rather than on anything this client controls, and it is not
            // stable across Redis builds. We therefore treat the fused ordering (and which document
            // carries the highest score) as don't-care, and assert only the contract this client owns:
            // the expected documents come back, each with a valid positive fused score, the internal
            // key field is not leaked, and typed mapping matches the untyped results.
            var heatId = $"{schema.Index.Prefix}1";
            var heatwaveId = $"{schema.Index.Prefix}2";

            Assert.True(results.Documents.Count >= 2);
            Assert.Contains(results.Documents, document => document.Id == heatId);
            Assert.Contains(results.Documents, document => document.Id == heatwaveId);

            foreach (var document in results.Documents)
            {
                Assert.False(document.Values.ContainsKey(HybridSearchQuery.KeyField));
                Assert.True(document.Values.ContainsKey(HybridSearchQuery.ScoreField));
                Assert.True(double.TryParse(
                    document.Values[HybridSearchQuery.ScoreField].ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var score));
                Assert.True(double.IsFinite(score) && score > 0);
            }

            Assert.Equal("Heat", results.Documents.Single(document => document.Id == heatId).Values["title"]);

            Assert.Equal(results.Documents.Count, typed.Documents.Count);
            Assert.Contains(typed.Documents, document => document.Id == heatId && document.Title == "Heat");
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisNativeHybridSearchIntegrationFact]
    public async Task ExecutesNativeHybridSearchQueriesWithVectorPreFilter()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var schema = new SearchSchema(
            new IndexDefinition($"native-hybrid-filter-idx-{token}", $"native-hybrid-filter:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre"),
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Flat,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        2))
            ]);
        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync();
            await SeedHashDocumentsAsync(database, schema, SearchIndexSeedData.HybridMovies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, SearchIndexSeedData.HybridMovies.Count);

            var query = HybridSearchQuery.FromFloat32(
                Filter.Text("title").Prefix("He"),
                "embedding",
                [0f, 1f],
                3,
                vectorFilter: Filter.Tag("genre").Eq("crime"),
                returnFields: ["title", "genre"]);

            var results = await index.SearchAsync(query);

            Assert.NotEmpty(results.Documents);
            Assert.All(results.Documents, document => Assert.Equal("crime", document.Values["genre"].ToString()));
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task FromExistingAsync_PreservesMultiFlagFieldsAcrossRoundTrip()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var originalName = $"multi-flag-idx-{token}";
        var roundTripName = $"multi-flag-roundtrip-idx-{token}";
        var schema = new SearchSchema(
            new IndexDefinition(originalName, $"multi-flag:{token}:", StorageType.Hash),
            [
                new TextFieldDefinition(
                    "title",
                    sortable: true,
                    noStem: true,
                    withSuffixTrie: true,
                    indexEmpty: true,
                    indexMissing: true,
                    unNormalizedForm: true),
                new TagFieldDefinition(
                    "genre",
                    sortable: true,
                    caseSensitive: true,
                    withSuffixTrie: true,
                    indexEmpty: true,
                    indexMissing: true),
                new NumericFieldDefinition("year", sortable: true, indexMissing: true)
            ]);
        var originalIndex = new SearchIndex(database, schema);
        SearchIndex? roundTripIndex = null;

        try
        {
            await originalIndex.CreateAsync(new CreateIndexOptions(overwrite: true));

            var reconstructed = await SearchIndex.FromExistingAsync(database, originalName);
            AssertMultiFlagFields(reconstructed.Schema);

            // Re-create a fresh index from the reconstructed schema and read it back:
            // a dropped flag on the first pass would be permanently lost here.
            roundTripIndex = new SearchIndex(
                database,
                new SearchSchema(
                    new IndexDefinition(roundTripName, $"multi-flag-roundtrip:{token}:", StorageType.Hash),
                    reconstructed.Schema.Fields));
            await roundTripIndex.CreateAsync(new CreateIndexOptions(overwrite: true));

            var roundTripped = await SearchIndex.FromExistingAsync(database, roundTripName);
            AssertMultiFlagFields(roundTripped.Schema);
        }
        finally
        {
            if (await originalIndex.ExistsAsync())
            {
                await originalIndex.DropAsync();
            }

            if (roundTripIndex is not null && await roundTripIndex.ExistsAsync())
            {
                await roundTripIndex.DropAsync();
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task FromExistingAsync_PreservesVectorIndexMissingAcrossRoundTrip()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var originalName = $"vec-flag-idx-{token}";
        var roundTripName = $"vec-flag-roundtrip-idx-{token}";
        var schema = new SearchSchema(
            new IndexDefinition(originalName, $"vec-flag:{token}:", StorageType.Hash),
            [
                new VectorFieldDefinition(
                    "embedding",
                    new VectorFieldAttributes(
                        VectorAlgorithm.Hnsw,
                        VectorDataType.Float32,
                        VectorDistanceMetric.Cosine,
                        4,
                        m: 8,
                        efConstruction: 100,
                        efRuntime: 50,
                        epsilon: 0.05),
                    indexMissing: true)
            ]);
        var originalIndex = new SearchIndex(database, schema);
        SearchIndex? roundTripIndex = null;

        try
        {
            await originalIndex.CreateAsync(new CreateIndexOptions(overwrite: true));

            var reconstructed = await SearchIndex.FromExistingAsync(database, originalName);
            AssertVectorIndexMissingField(reconstructed.Schema);

            // Re-create a fresh index from the reconstructed schema: a dropped
            // INDEXMISSING flag on the first pass would be permanently lost here.
            roundTripIndex = new SearchIndex(
                database,
                new SearchSchema(
                    new IndexDefinition(roundTripName, $"vec-flag-roundtrip:{token}:", StorageType.Hash),
                    reconstructed.Schema.Fields));
            await roundTripIndex.CreateAsync(new CreateIndexOptions(overwrite: true));

            var roundTripped = await SearchIndex.FromExistingAsync(database, roundTripName);
            AssertVectorIndexMissingField(roundTripped.Schema);
        }
        finally
        {
            if (await originalIndex.ExistsAsync())
            {
                await originalIndex.DropAsync();
            }

            if (roundTripIndex is not null && await roundTripIndex.ExistsAsync())
            {
                await roundTripIndex.DropAsync();
            }
        }
    }

    private static void AssertVectorIndexMissingField(SearchSchema schema)
    {
        var vectorField = Assert.IsType<VectorFieldDefinition>(schema.Fields.Single(static field => field.Name == "embedding"));
        Assert.True(vectorField.IndexMissing);
        Assert.Equal(VectorAlgorithm.Hnsw, vectorField.Attributes.Algorithm);
    }

    private static void AssertMultiFlagFields(SearchSchema schema)
    {
        var textField = Assert.IsType<TextFieldDefinition>(schema.Fields.Single(static field => field.Name == "title"));
        Assert.True(textField.Sortable);
        Assert.True(textField.NoStem);
        Assert.True(textField.WithSuffixTrie);
        Assert.True(textField.IndexEmpty);
        Assert.True(textField.IndexMissing);

        var tagField = Assert.IsType<TagFieldDefinition>(schema.Fields.Single(static field => field.Name == "genre"));
        Assert.True(tagField.Sortable);
        Assert.True(tagField.CaseSensitive);
        Assert.True(tagField.WithSuffixTrie);
        Assert.True(tagField.IndexEmpty);
        Assert.True(tagField.IndexMissing);

        var numericField = Assert.IsType<NumericFieldDefinition>(schema.Fields.Single(static field => field.Name == "year"));
        Assert.True(numericField.Sortable);
        Assert.True(numericField.IndexMissing);
    }

    private static async Task SeedHashDocumentsAsync(
        IDatabase database,
        SearchSchema schema,
        IEnumerable<SearchIndexSeedData.HashSeedDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(documents);

        foreach (var document in documents)
        {
            await database.HashSetAsync($"{schema.Index.Prefix}{document.Id}", document.Entries);
        }
    }
}
