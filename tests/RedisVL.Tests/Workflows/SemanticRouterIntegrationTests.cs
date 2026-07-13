using RedisVL.Schema;
using RedisVL.Tests.Indexes;
using RedisVL.Workflows;
using StackExchange.Redis;

namespace RedisVL.Tests.Workflows;

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

    [RedisSearchIntegrationFact]
    public async Task RouteManyAggregatesReferencesAndRanksRoutes()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var router = new SemanticRouter(database, CreateOptions(token, 0.5d));

        try
        {
            await router.CreateAsync();
            await router.AddRouteAsync(
                new Route("billing", ["refund", "payment"]),
                new[] { new[] { 1f, 0f }, new[] { 0.9f, 0.1f } });
            await router.AddRouteAsync(
                new Route("shipping", ["delivery"]),
                new[] { new[] { 0.8f, 0.2f } });
            await RedisSearchTestEnvironment.WaitForAsync(
                async () =>
                {
                    var ready = await router.RouteManyAsync("query", [1f, 0f], maxResults: 2);
                    return ready.Count == 2;
                });

            var matches = await router.RouteManyAsync("query", [1f, 0f], maxResults: 2);

            Assert.Equal(2, matches.Count);
            Assert.Equal("billing", matches[0].RouteName);
            Assert.Equal("shipping", matches[1].RouteName);
            Assert.True(matches[0].Distance <= matches[1].Distance);
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
    public async Task AddsGetsAndDeletesRouteReferences()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var router = new SemanticRouter(database, CreateOptions(token, 0.5d));

        try
        {
            await router.CreateAsync();
            await router.AddRouteAsync(
                new Route("billing", ["refund", "payment"]),
                new[] { new[] { 1f, 0f }, new[] { 0.9f, 0.1f } });
            await router.AddRouteReferencesAsync("billing", ["chargeback"], new[] { new[] { 0.95f, 0.05f } });
            await RedisSearchTestEnvironment.WaitForAsync(
                async () =>
                {
                    var references = await router.GetRouteReferencesAsync("billing");
                    return references.Count == 3;
                });

            var references = await router.GetRouteReferencesAsync("billing");
            Assert.Equal(3, references.Count);
            Assert.All(references, reference => Assert.Equal("billing", reference.RouteName));

            var removed = await router.DeleteRouteReferencesAsync("billing", ["chargeback"]);
            Assert.Equal(1, removed);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => (await router.GetRouteReferencesAsync("billing")).Count == 2);

            var deletedRoute = await router.DeleteRouteAsync("billing");
            Assert.Equal(2, deletedRoute);
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => (await router.GetRouteReferencesAsync("billing")).Count == 0);
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
    public async Task GetRoutePreservesMetadataAndPerRouteThreshold()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var router = new SemanticRouter(database, CreateOptions(token, 0.5d));

        try
        {
            await router.CreateAsync();
            await router.AddRouteAsync(
                new Route(
                    "billing",
                    ["refund"],
                    new Dictionary<string, object?> { ["team"] = "finance" },
                    distanceThreshold: 0.2d),
                new[] { new[] { 1f, 0f } });
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => await router.GetRouteAsync("billing") is not null);

            var route = await router.GetRouteAsync("billing");

            Assert.NotNull(route);
            Assert.Equal(0.2d, route!.DistanceThreshold);
            Assert.NotNull(route.Metadata);
            Assert.True(route.Metadata!.ContainsKey("team"));
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
    public async Task PerRouteThresholdHonoredForReferencesAddedLaterRegardlessOfNearestReference()
    {
        await using var connection = await RedisSearchTestEnvironment.ConnectAsync();
        var database = connection.GetDatabase();

        var token = Guid.NewGuid().ToString("N");
        var router = new SemanticRouter(database, CreateOptions(token, 0.5d));

        try
        {
            await router.CreateAsync();

            // billing carries a strict per-route threshold (0.05). Its first reference is at [1,0]; a second
            // is added *later* via AddRouteReferencesAsync, which previously wrote a null threshold and let the
            // route fall back to the router default whenever that later reference was the nearest match.
            await router.AddRouteAsync(
                new Route("billing", ["refund status"], distanceThreshold: 0.05d),
                new[] { new[] { 1f, 0f } });
            await router.AddRouteReferencesAsync("billing", ["late reference"], new[] { new[] { 0f, 1f } });
            await RedisSearchTestEnvironment.WaitForAsync(
                async () => (await router.GetRouteReferencesAsync("billing")).Count == 2);

            // A query nearest the late reference sits at squared-L2 distance ~0.1 from it: inside the router
            // default (0.5) but outside billing's per-route threshold (0.05). The route must be rejected.
            var nearLate = new[] { 0f, 0.684f };
            var many = await router.RouteManyAsync("late question", nearLate, maxResults: 5);
            var single = await router.RouteAsync("late question", nearLate);

            Assert.Empty(many);
            Assert.Null(single);

            // The per-route threshold still admits inputs genuinely within it (the original reference).
            var withinThreshold = await router.RouteManyAsync("refund status", [1f, 0f], maxResults: 5);
            Assert.Single(withinThreshold);
            Assert.Equal("billing", withinThreshold[0].RouteName);
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
