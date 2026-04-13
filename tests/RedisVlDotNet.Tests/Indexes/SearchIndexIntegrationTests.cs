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

    private sealed record JsonMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record JsonMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record HashMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

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
