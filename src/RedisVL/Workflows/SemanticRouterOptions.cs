using RedisVL.Schema;

namespace RedisVL.Workflows;

public sealed class SemanticRouterOptions
{
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

    public string Name { get; }

    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    /// <summary>
    /// Gets the maximum routing radius. A route only matches when its (aggregated) distance is within this
    /// threshold; per-route thresholds may restrict matching further but cannot exceed this value.
    /// </summary>
    public double DistanceThreshold { get; }

    public string? KeyNamespace { get; }

    public string RouteNameFieldName { get; }

    public string ReferenceFieldName { get; }

    public string EmbeddingFieldName { get; }

    /// <summary>Gets the multi-match routing configuration (max results and distance aggregation method).</summary>
    public RoutingConfig RoutingConfig { get; }
}
