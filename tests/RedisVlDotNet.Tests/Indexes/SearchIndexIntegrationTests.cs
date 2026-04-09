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
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
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
    public async Task LoadsFetchesAndDeletesJsonDocuments()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
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
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
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
    public async Task ExecutesVectorQueriesWithDeterministicRanking()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(RedisSearchTestEnvironment.ConnectionString!);
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

            await database.HashSetAsync(
                $"{schema.Index.Prefix}1",
                [
                    new HashEntry("title", "Heat"),
                    new HashEntry("genre", "crime"),
                    new HashEntry("embedding", EncodeFloat32([1f, 0f]))
                ]);
            await database.HashSetAsync(
                $"{schema.Index.Prefix}2",
                [
                    new HashEntry("title", "Thief"),
                    new HashEntry("genre", "crime"),
                    new HashEntry("embedding", EncodeFloat32([0.8f, 0.2f]))
                ]);
            await database.HashSetAsync(
                $"{schema.Index.Prefix}3",
                [
                    new HashEntry("title", "Arrival"),
                    new HashEntry("genre", "science-fiction"),
                    new HashEntry("embedding", EncodeFloat32([0f, 1f]))
                ]);

            await Task.Delay(TimeSpan.FromMilliseconds(250));

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

    private sealed record JsonMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record JsonMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

    private sealed record HashMovieDocument(string Id, string Title, int Year, string Genre);

    private sealed record HashMovieEnvelope(string ExternalId, string Title, int Year, string Genre);

    private static byte[] EncodeFloat32(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
