using RedisVl.Schema;
using RedisVl.Tests.Indexes;
using RedisVl.Workflows;
using StackExchange.Redis;

namespace RedisVl.Tests.Workflows;

public sealed class SemanticRouterIntegrationTests
{
    [RedisSearchIntegrationFact]
    public async Task CreatesRoutesAndMatchesNearestRoute()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var router = new SemanticRouter(database, CreateOptions(token, 0.25d));

        try
        {
            await router.CreateAsync();
            await router.AddRouteAsync("billing", "refund status", [1f, 0f]);
            await router.AddRouteAsync("support", "technical troubleshooting", [0f, 1f]);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () =>
                {
                    var ready = await router.RouteAsync("refund status", [1f, 0f]);
                    return ready is not null;
                });

            var match = await router.RouteAsync("where is my refund?", [1.1f, 0f]);

            Assert.NotNull(match);
            Assert.Equal("billing", match!.RouteName);
            Assert.Equal("refund status", match.Reference);
            Assert.InRange(match.Distance, 0d, 0.25d);
        }
        finally
        {
            if (await router.ExistsAsync())
            {
                await router.DropAsync(deleteDocuments: true);
            }
        }
    }

    [RedisSearchIntegrationFact]
    public async Task ReturnsMissWhenNearestRouteFallsOutsideThreshold()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var router = new SemanticRouter(database, CreateOptions(token, 0.2d));

        try
        {
            await router.CreateAsync();
            await router.AddRouteAsync("billing", "refund status", [1f, 0f]);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () =>
                {
                    var ready = await router.RouteAsync("refund status", [1f, 0f]);
                    return ready is not null;
                });

            var miss = await router.RouteAsync("reset my password", [0f, 1f]);

            Assert.Null(miss);
        }
        finally
        {
            if (await router.ExistsAsync())
            {
                await router.DropAsync(deleteDocuments: true);
            }
        }
    }

    private static SemanticRouterOptions CreateOptions(string token, double distanceThreshold) =>
        new(
            "integration-semantic-router",
            new VectorFieldAttributes(
                VectorAlgorithm.Flat,
                VectorDataType.Float32,
                VectorDistanceMetric.L2,
                2),
            distanceThreshold,
            token);
}
