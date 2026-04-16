using RedisVl.Indexes;
using RedisVl.Queries;
using RedisVl.Schema;

namespace RedisVl.Tests.Indexes;

public sealed class RedisSentinelConnectionIntegrationTests
{
    [RedisSentinelIntegrationFact]
    public async Task ConnectSentinelPrimaryAsync_CanCreateAndQuerySearchIndex()
    {
        await using var connection = await RedisSentinelTestEnvironment.ConnectPrimaryAsync();
        var database = connection.GetDatabase();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var schema = new SearchSchema(
            new IndexDefinition($"sentinel-movies-{suffix}", $"sentinel-movie:{suffix}:", StorageType.Json),
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
                    new SentinelMovie("movie-1", "Arrival", "science-fiction"),
                    new SentinelMovie("movie-2", "Heat", "crime")
                ]);

            await RedisSearchTestEnvironment.WaitForIndexDocumentCountAsync(index, 2);

            var results = await index.SearchAsync<SentinelMovie>(
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

    private sealed record SentinelMovie(string Id, string Title, string Genre);
}
