using RedisVL.Indexes;
using RedisVL.Queries;
using RedisVL.Schema;

namespace RedisVL.Tests.Indexes;

public sealed class RedisClusterConnectionIntegrationTests
{
    [RedisClusterIntegrationFact]
    public async Task ConnectClusterAsync_CanCreateAndQuerySearchIndex()
    {
        await using var connection = await RedisClusterTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schema = new SearchSchema(
            new IndexDefinition($"cluster-movies-{suffix}", $"cluster-movie:{suffix}:", StorageType.Json),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre")
            ]);

        var index = new SearchIndex(database, schema);

        try
        {
            await index.CreateAsync(new CreateIndexOptions(skipIfExists: true));
            await index.LoadJsonAsync(
                [
                    new ClusterMovie("movie-1", "Arrival", "science-fiction"),
                    new ClusterMovie("movie-2", "Heat", "crime")
                ]);

            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 2);

            var results = await index.SearchAsync<ClusterMovie>(
                new TextQuery("Arrival", ["title", "genre"], limit: 1));

            Assert.Equal(1, results.TotalCount);
            Assert.Equal("Arrival", Assert.Single(results.Documents).Title);
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisClusterIntegrationFact]
    public async Task ClearAsync_DeletesKeysAcrossShardsWithoutCrossSlot()
    {
        await using var connection = await RedisClusterTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schema = new SearchSchema(
            new IndexDefinition($"cluster-clear-{suffix}", $"cluster-clear:{suffix}:", StorageType.Json),
            [
                new TextFieldDefinition("title"),
                new TagFieldDefinition("genre")
            ]);

        var index = new SearchIndex(database, schema);

        // Distinct un-tagged ids so the documents hash to a spread of slots
        // across shards, which a single-node SCAN would miss and a multi-key
        // DEL would reject with CROSSSLOT.
        var movies = Enumerable.Range(0, 12)
            .Select(i => new ClusterMovie($"movie-{i}", $"Title {i}", i % 2 == 0 ? "crime" : "science-fiction"))
            .ToArray();

        try
        {
            await index.CreateAsync(new CreateIndexOptions(skipIfExists: true));
            await index.LoadJsonAsync(movies);
            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, movies.Length);

            var deletedCount = await index.ClearAsync();

            await RedisSearchTestEnvironment.WaitForAsync(async () => await index.CountAsync(new CountQuery()) == 0);

            Assert.Equal(movies.Length, deletedCount);
            Assert.True(await index.ExistsAsync());
        }
        finally
        {
            if (await index.ExistsAsync())
            {
                await index.DropAsync(deleteDocuments: true);
            }
        }
    }

    private sealed record ClusterMovie(string Id, string Title, string Genre);
}
