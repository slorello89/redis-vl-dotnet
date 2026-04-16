using RedisVl.Indexes;
using RedisVl.Schema;
using RedisVl.Tests.Indexes;
using RedisVl.Workflows;

namespace RedisVl.Tests.Workflows;

public sealed class SemanticMessageHistoryIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task AppendsMessagesAndRetrievesSemanticSessionHistory()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var history = new SemanticMessageHistory(
            database,
            new SemanticMessageHistoryOptions(
                "integration-semantic-history",
                new VectorFieldAttributes(
                    VectorAlgorithm.Flat,
                    VectorDataType.Float32,
                    VectorDistanceMetric.Cosine,
                    2),
                0.25d,
                token));

        try
        {
            await history.CreateAsync(new CreateIndexOptions(overwrite: true, dropExistingDocuments: true));
            await history.AppendAsync(
                "session-a",
                "user",
                "refund request",
                [1f, 0f],
                new { turn = 1 },
                DateTimeOffset.Parse("2026-04-13T13:00:00Z"));
            await history.AppendAsync(
                "session-a",
                "assistant",
                "refund status",
                [0.99f, 0.01f],
                new { turn = 2 },
                DateTimeOffset.Parse("2026-04-13T13:00:01Z"));
            await history.AppendAsync(
                "session-a",
                "assistant",
                "shipping update",
                [0f, 1f],
                new { turn = 3 },
                DateTimeOffset.Parse("2026-04-13T13:00:02Z"));
            await history.AppendAsync(
                "session-b",
                "assistant",
                "other session result",
                [1f, 0f],
                new { turn = 99 },
                DateTimeOffset.Parse("2026-04-13T13:00:03Z"));

            await RedisSearchTestEnvironment.WaitForAsync(async () =>
            {
                var matches = await history.GetRelevantAsync("session-a", [1f, 0f], limit: 2);
                return matches.Count >= 1;
            });

            var relevant = await history.GetRelevantAsync("session-a", [1f, 0f], limit: 2);
            var assistantOnly = await history.GetRelevantAsync("session-a", [1f, 0f], limit: 5, role: "assistant");
            var strictThreshold = await history.GetRelevantAsync("session-a", [1f, 0f], limit: 5, distanceThreshold: 0.001d);

            Assert.Equal(["refund request", "refund status"], relevant.Select(static match => match.Message.Content).ToArray());
            Assert.All(relevant, static match => Assert.Equal("session-a", match.Message.SessionId));
            Assert.Equal(["refund status"], assistantOnly.Select(static match => match.Message.Content).ToArray());
            Assert.Equal(["refund request", "refund status"], strictThreshold.Select(static match => match.Message.Content).ToArray());
            Assert.True(relevant[0].Distance <= relevant[1].Distance);
            Assert.Equal("{\"turn\":1}", relevant[0].Message.Metadata);
        }
        finally
        {
            if (await history.ExistsAsync())
            {
                await history.DropAsync(deleteDocuments: true);
            }
        }
    }
}
