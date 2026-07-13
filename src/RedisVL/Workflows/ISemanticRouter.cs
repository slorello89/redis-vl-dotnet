using RedisVL.Indexes;
using RedisVL.Vectorizers;

namespace RedisVL.Workflows;

/// <summary>
/// Abstraction over a semantic router, mirroring the public surface of <see cref="SemanticRouter" />.
/// Depend on this interface where the router needs to be substituted in unit tests.
/// </summary>
public interface ISemanticRouter
{
    /// <summary>Gets the configuration this router was created with.</summary>
    SemanticRouterOptions Options { get; }

    /// <summary>Gets the router name (from <see cref="Options" />).</summary>
    string Name { get; }

    /// <summary>Gets the optional key namespace (from <see cref="Options" />), or <see langword="null" /> when unset.</summary>
    string? KeyNamespace { get; }

    /// <summary>Gets the default maximum vector distance for a reference to be considered a match (from <see cref="Options" />).</summary>
    double DistanceThreshold { get; }

    /// <summary>Gets the routing configuration controlling aggregation and candidate limits (from <see cref="Options" />).</summary>
    RoutingConfig RoutingConfig { get; }

    /// <summary>Creates the router's underlying search index.</summary>
    Task<bool> CreateAsync(CreateIndexOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Determines whether the router's underlying search index exists.</summary>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>Drops the router's underlying search index, optionally deleting the stored references.</summary>
    Task DropAsync(bool deleteDocuments = false, CancellationToken cancellationToken = default);

    /// <summary>Adds a single reference to a route using a precomputed embedding and returns the stored key.</summary>
    Task<string> AddRouteAsync(
        string routeName,
        string reference,
        float[] embedding,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a single reference to a route, vectorizing it with the supplied vectorizer, and returns the stored key.</summary>
    Task<string> AddRouteAsync(
        string routeName,
        string reference,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a route's references using precomputed embeddings; returned keys are aligned to the route's references.</summary>
    Task<IReadOnlyList<string>> AddRouteAsync(
        Route route,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a route's references, vectorizing them in a single batch via the supplied vectorizer.</summary>
    Task<IReadOnlyList<string>> AddRouteAsync(
        Route route,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    /// <summary>Adds references to an existing route using precomputed embeddings; returned keys are aligned to input order.</summary>
    Task<IReadOnlyList<string>> AddRouteReferencesAsync(
        string routeName,
        IReadOnlyList<string> references,
        IReadOnlyList<float[]> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>Adds references to an existing route, vectorizing them in a single batch via the supplied vectorizer.</summary>
    Task<IReadOnlyList<string>> AddRouteReferencesAsync(
        string routeName,
        IReadOnlyList<string> references,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the stored references for a route, or an empty list when it has none.</summary>
    Task<IReadOnlyList<RouteReference>> GetRouteReferencesAsync(
        string routeName,
        CancellationToken cancellationToken = default);

    /// <summary>Reconstructs a route from its stored references, or <see langword="null" /> when it has none.</summary>
    Task<Route?> GetRouteAsync(string routeName, CancellationToken cancellationToken = default);

    /// <summary>Deletes specific references from a route and returns the number actually removed.</summary>
    Task<long> DeleteRouteReferencesAsync(
        string routeName,
        IEnumerable<string> references,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes every reference belonging to a route and returns the number removed.</summary>
    Task<long> DeleteRouteAsync(string routeName, CancellationToken cancellationToken = default);

    /// <summary>Classifies an input using a precomputed embedding and returns the single nearest route match, or <see langword="null" /> on a miss.</summary>
    Task<SemanticRouteMatch?> RouteAsync(
        string input,
        float[] embedding,
        CancellationToken cancellationToken = default);

    /// <summary>Classifies an input, vectorizing it with the supplied vectorizer, and returns the single nearest route match, or <see langword="null" /> on a miss.</summary>
    Task<SemanticRouteMatch?> RouteAsync(
        string input,
        ITextVectorizer vectorizer,
        CancellationToken cancellationToken = default);

    /// <summary>Classifies an input across all routes using a precomputed embedding and returns matches ordered nearest-first.</summary>
    Task<IReadOnlyList<SemanticRouterMatch>> RouteManyAsync(
        string input,
        float[] embedding,
        int? maxResults = null,
        DistanceAggregationMethod? aggregationMethod = null,
        CancellationToken cancellationToken = default);

    /// <summary>Classifies an input across all routes, vectorizing it with the supplied vectorizer, and returns matches ordered nearest-first.</summary>
    Task<IReadOnlyList<SemanticRouterMatch>> RouteManyAsync(
        string input,
        ITextVectorizer vectorizer,
        int? maxResults = null,
        DistanceAggregationMethod? aggregationMethod = null,
        CancellationToken cancellationToken = default);
}
