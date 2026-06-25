using Microsoft.Extensions.AI;
using RedisVL.Connectors.VectorData.ChatMemory;
using RedisVL.Tests.Indexes;
using RedisVL.Workflows;
using StackExchange.Redis;

namespace RedisVL.Tests.Connectors.VectorData;

public sealed class RedisVLChatMessageStoreIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task AddAndGetMessages_RoundTripsInChronologicalOrder()
    {
        var multiplexer = await RedisSearchTestEnvironment.ConnectAsync();
        try
        {
            var options = new MessageHistoryOptions($"vectordata-chat-{Guid.NewGuid():N}");
            var store = new RedisVLChatMessageStore(multiplexer.GetDatabase(), options);
            await store.CreateAsync();

            var sessionId = "session-1";
            await store.AddMessagesAsync(sessionId,
            [
                new ChatMessage(ChatRole.User, "Recommend a sci-fi movie."),
                new ChatMessage(ChatRole.Assistant, "Try Arrival."),
                new ChatMessage(ChatRole.User, "Anything older?"),
                new ChatMessage(ChatRole.Assistant, string.Empty), // content-free messages are skipped
            ]);

            var messages = await store.GetMessagesAsync(sessionId);

            Assert.Equal(3, messages.Count);
            Assert.Equal(ChatRole.User, messages[0].Role);
            Assert.Equal("Recommend a sci-fi movie.", messages[0].Text);
            Assert.Equal(ChatRole.Assistant, messages[1].Role);
            Assert.Equal("Try Arrival.", messages[1].Text);
            Assert.Equal("Anything older?", messages[2].Text);

            await store.History.DropAsync(deleteDocuments: true);
        }
        finally
        {
            await multiplexer.CloseAsync();
            multiplexer.Dispose();
        }
    }
}
