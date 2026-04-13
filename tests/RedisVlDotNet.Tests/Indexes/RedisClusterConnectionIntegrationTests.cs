using RedisVlDotNet.Indexes;
using RedisVlDotNet.Queries;
using RedisVlDotNet.Schema;

namespace RedisVlDotNet.Tests.Indexes;

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

    private sealed record ClusterMovie(string Id, string Title, string Genre);
}
