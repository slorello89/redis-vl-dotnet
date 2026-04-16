using RedisVl.Tests.Indexes;
using RedisVl.Workflows;

namespace RedisVl.Tests.Workflows;

public sealed class MessageHistoryIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task AppendsMessagesAndRetrievesRecentSessionHistory()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var history = new MessageHistory(database, new MessageHistoryOptions("integration-message-history", token));

        try
        {
            await history.CreateAsync(new RedisVl.Indexes.CreateIndexOptions(overwrite: true, dropExistingDocuments: true));
            await history.AppendAsync(
                "session-a",
                "user",
                "Hello",
                new { turn = 1 },
                DateTimeOffset.Parse("2026-04-13T13:00:00Z"));
            await history.AppendAsync(
                "session-a",
                "assistant",
                "Hi there",
                new { turn = 2 },
                DateTimeOffset.Parse("2026-04-13T13:00:01Z"));
            await history.AppendAsync(
                "session-b",
                "assistant",
                "Other session",
                new { turn = 99 },
                DateTimeOffset.Parse("2026-04-13T13:00:02Z"));
            await history.AppendAsync(
                "session-a",
                "assistant",
                "Need anything else?",
                new { turn = 3 },
                DateTimeOffset.Parse("2026-04-13T13:00:03Z"));

            await RedisSearchTestEnvironment.WaitForAsync(async () =>
            {
                var messages = await history.GetRecentAsync("session-a", 3);
                return messages.Count == 3;
            });

            var recent = await history.GetRecentAsync("session-a", 3);
            var assistantOnly = await history.GetRecentAsync("session-a", 2, role: "assistant");

            Assert.Equal(["Need anything else?", "Hi there", "Hello"], recent.Select(static message => message.Content).ToArray());
            Assert.Equal(["assistant", "assistant", "user"], recent.Select(static message => message.Role).ToArray());
            Assert.All(recent, static message => Assert.Equal("session-a", message.SessionId));
            Assert.Equal(["Need anything else?", "Hi there"], assistantOnly.Select(static message => message.Content).ToArray());
            Assert.All(assistantOnly, static message => Assert.Equal("assistant", message.Role));
            Assert.Equal("{\"turn\":3}", recent[0].Metadata);
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
