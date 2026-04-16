using RedisVl.Schema;

namespace RedisVl.Workflows;

public sealed class SemanticRouterOptions
{
    public SemanticRouterOptions(
        string name,
        VectorFieldAttributes embeddingFieldAttributes,
        double distanceThreshold,
        string? keyNamespace = null,
        string routeNameFieldName = "routeName",
        string referenceFieldName = "reference",
        string embeddingFieldName = "embedding")
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
    }

    public string Name { get; }

    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    public double DistanceThreshold { get; }

    public string? KeyNamespace { get; }

    public string RouteNameFieldName { get; }

    public string ReferenceFieldName { get; }

    public string EmbeddingFieldName { get; }
}
