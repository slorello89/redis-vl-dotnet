using RedisVL.Indexes;
using RedisVL.Vectorizers;

namespace RedisVL.Workflows;

/// <summary>
/// Abstraction over a semantic router, mirroring the public surface of <see cref="SemanticRouter" />.
/// Depend on this interface where the router needs to be substituted in unit tests.
/// </summary>
public interface ISemanticRouter
{
    SemanticRouterOptions Options { get; }

    string Name { get; }

    string? KeyNamespace { get; }

    double DistanceThreshold { get; }

    RoutingConfig RoutingConfig { get; }

    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    Task<string> AddRouteAsync(
        string routeName,
        string reference,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<string> AddRouteAsync(
        string routeName,
        string reference,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> AddRouteAsync(
        Route route,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> AddRouteAsync(
        Route route,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> AddRouteReferencesAsync(
        string routeName,
        IReadOnlyList<string> references,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> AddRouteReferencesAsync(
        string routeName,
        IReadOnlyList<string> references,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RouteReference>> GetRouteReferencesAsync(
        string routeName,
        CancellationToken cancellationToken = default);

    Task<Route?> GetRouteAsync(string routeName, CancellationToken cancellationToken = default);

    Task<long> DeleteRouteReferencesAsync(
        string routeName,
        IEnumerable<string> references,
        CancellationToken cancellationToken = default);

    Task<long> DeleteRouteAsync(string routeName, CancellationToken cancellationToken = default);

    Task<SemanticRouteMatch?> RouteAsync(
        string input,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<SemanticRouteMatch?> RouteAsync(
        string input,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticRouterMatch>> RouteManyAsync(
        string input,
        float[] embedding,
        int? maxResults = null,
        DistanceAggregationMethod? aggregationMethod = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SemanticRouterMatch>> RouteManyAsync(
        string input,
        ITextVectorizer vectorizer,
        int? maxResults = null,
        DistanceAggregationMethod? aggregationMethod = null,
        CancellationToken cancellationToken = default);
}
