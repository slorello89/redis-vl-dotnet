using RedisVlDotNet.Indexes;
using RedisVlDotNet.Filters;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Tests.Indexes;

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
            Assert.Equal(schema.Index.KeySeparator, reconnectedIndex.Schema.Index.KeySeparator);
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

            Assert.Equal("|", definition["separator"]);
            Assert.Equal(["the", "a", "an"], stopwords);
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
                noFields: true,
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
                    indexMissing: true,
                    indexEmpty: true,
                    noIndex: true),
                new NumericFieldDefinition("rating", sortable: true, indexMissing: true, noIndex: true, unNormalizedForm: true),
                new GeoFieldDefinition("location", sortable: true, indexMissing: true, noIndex: true),
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
            Assert.Contains("NOFIELDS", indexOptions);
            Assert.Contains("NOFREQS", indexOptions);
            Assert.Contains("MAXTEXTFIELDS", indexOptions);
            Assert.Contains("SKIPINITIALSCAN", indexOptions);
            Assert.Contains("TEMPORARY", indexOptions);
            Assert.Contains("300", indexOptions);

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

            Assert.Single(results.Documents);
            Assert.Equal("Heat", results.Documents[0].Title);
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
                Filter.Geo("location").WithinRadius(-118.2437, 34.0522, 50, RedisVlDotNet.Filters.GeoUnit.Miles),
                ["title"],
                limit: 10));
            var crimeCount = await index.CountAsync(new CountQuery(Filter.Tag("genre").Eq("crime")));

            Assert.Equal(2, tagResults.TotalCount);
            Assert.Equal(
                [$"{schema.Index.Prefix}1", $"{schema.Index.Prefix}2"],
                tagResults.Documents.Select(static document => document.Id).ToArray());
            Assert.Equal(2, numericResults.TotalCount);
            Assert.Equal(
                [$"{schema.Index.Prefix}1", $"{schema.Index.Prefix}3"],
                numericResults.Documents.Select(static document => document.Id).ToArray());
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

            Assert.Equal(2, results.TotalCount);
            Assert.Equal(2, results.Documents.Count);
            Assert.Equal($"{schema.Index.Prefix}1", results.Documents[0].Id);
            Assert.Equal($"{schema.Index.Prefix}2", results.Documents[1].Id);
            Assert.Equal("Heat Heat", results.Documents[0].Values["title"]);
            Assert.Equal("1995", results.Documents[0].Values["year"]);
            Assert.False(results.Documents[0].TryGetValue("genre", out _));
            Assert.Equal("Heat", results.Documents[1].Values["title"]);
            Assert.Equal("1981", results.Documents[1].Values["year"]);
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

            Assert.Equal(2, rawResults.TotalCount);
            Assert.Equal(["crime", "science-fiction"], rawResults.Rows.Select(static row => row.Values["genre"].ToString()).ToArray());
            Assert.Equal(["2", "1"], rawResults.Rows.Select(static row => row.Values["movieCount"].ToString()).ToArray());
            Assert.Equal(["1988", "2016"], rawResults.Rows.Select(static row => row.Values["averageYear"].ToString()).ToArray());

            Assert.Equal(2, typedResults.TotalCount);
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

            Assert.Equal(2, rawResults.TotalCount);
            Assert.Equal(["crime", "science-fiction"], rawResults.Rows.Select(static row => row.Values["genre"].ToString()).ToArray());
            Assert.Equal(["2", "1"], rawResults.Rows.Select(static row => row.Values["matchCount"].ToString()).ToArray());

            Assert.Equal(2, typedResults.TotalCount);
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
            Assert.True(double.Parse(results.Documents[0].Values["distance"]!, System.Globalization.CultureInfo.InvariantCulture) <
                        double.Parse(results.Documents[1].Values["distance"]!, System.Globalization.CultureInfo.InvariantCulture));
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
            Assert.True(double.Parse(results.Documents[0].Values["distance"]!, System.Globalization.CultureInfo.InvariantCulture) <
                        double.Parse(results.Documents[1].Values["distance"]!, System.Globalization.CultureInfo.InvariantCulture));
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
            Assert.True(double.Parse(results.Documents[0].Values["distance"]!, System.Globalization.CultureInfo.InvariantCulture) <
                        double.Parse(results.Documents[1].Values["distance"]!, System.Globalization.CultureInfo.InvariantCulture));
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
                .Select(document => double.Parse(document.Values["combined_distance"]!, System.Globalization.CultureInfo.InvariantCulture))
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

    private sealed record JsonMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record JsonMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

    private sealed record JsonMovieWithMetadata(string Id, string Title, int Year, string Genre, JsonMovieMetadata Metadata);

    private sealed record JsonMovieMetadata(string Director, double Rating);

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record HashMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

    private sealed record GenreAggregationRow(string Genre, int MovieCount, double AverageYear);

    private sealed record HybridAggregationRow(string Genre, int MatchCount, double AvgDistance);

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
