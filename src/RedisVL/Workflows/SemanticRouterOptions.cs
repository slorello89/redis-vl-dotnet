using RedisVL.Schema;

namespace RedisVL.Workflows;

/// <summary>Configuration for a <see cref="SemanticRouter" />, including index naming, field names, thresholds, and routing behavior.</summary>
public sealed class SemanticRouterOptions
{
    /// <summary>Initializes a new <see cref="SemanticRouterOptions" />.</summary>
    /// <param name="name">The router name, used to derive the index name and key prefix.</param>
    /// <param name="embeddingFieldAttributes">The vector field attributes describing the reference embeddings.</param>
    /// <param name="distanceThreshold">The default maximum routing distance; must be greater than zero.</param>
    /// <param name="keyNamespace">An optional namespace that isolates this router's keys and index from others sharing a name.</param>
    /// <param name="routeNameFieldName">The hash field and index field storing the route name.</param>
    /// <param name="referenceFieldName">The hash field and index field storing the reference phrase.</param>
    /// <param name="embeddingFieldName">The hash field and index field storing the reference embedding.</param>
    /// <param name="routingConfig">The multi-match routing configuration, or <see langword="null" /> to use defaults.</param>
    /// <exception cref="ArgumentException">A required name argument is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="distanceThreshold" /> is not greater than zero.</exception>
    public SemanticRouterOptions(
        string name,
        VectorFieldAttributes embeddingFieldAttributes,
        double distanceThreshold,
        string? keyNamespace = null,
        string routeNameFieldName = "routeName",
        string referenceFieldName = "reference",
        string embeddingFieldName = "embedding",
        RoutingConfig? routingConfig = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(embeddingFieldAttributes);
        ArgumentException.ThrowIfNullOrWhiteSpace(routeNameFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingFieldName);

        if (distanceThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), distanceThreshold, "Semantic router distance threshold must be greater than zero.");
        }

        Name = name.Trim();
        EmbeddingFieldAttributes = embeddingFieldAttributes;
        DistanceThreshold = distanceThreshold;
        KeyNamespace = string.IsNullOrWhiteSpace(keyNamespace) ? null : keyNamespace.Trim();
        RouteNameFieldName = routeNameFieldName.Trim();
        ReferenceFieldName = referenceFieldName.Trim();
        EmbeddingFieldName = embeddingFieldName.Trim();
        RoutingConfig = routingConfig ?? new RoutingConfig();
    }

    /// <summary>Gets the router name, used to derive the index name and key prefix.</summary>
    public string Name { get; }

    /// <summary>Gets the vector field attributes describing the stored reference embeddings.</summary>
    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    /// <summary>
    /// Gets the maximum routing radius. A route only matches when its (aggregated) distance is within this
    /// threshold; per-route thresholds may restrict matching further but cannot exceed this value.
    /// </summary>
    public double DistanceThreshold { get; }

    /// <summary>Gets the optional namespace that isolates this router's keys and index from others sharing a name.</summary>
    public string? KeyNamespace { get; }

    /// <summary>Gets the field name storing the route name.</summary>
    public string RouteNameFieldName { get; }

    /// <summary>Gets the field name storing the reference phrase.</summary>
    public string ReferenceFieldName { get; }

    /// <summary>Gets the field name storing the reference embedding.</summary>
    public string EmbeddingFieldName { get; }

    /// <summary>Gets the multi-match routing configuration (max results and distance aggregation method).</summary>
    public RoutingConfig RoutingConfig { get; }
}
