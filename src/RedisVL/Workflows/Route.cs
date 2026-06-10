namespace RedisVL.Workflows;

/// <summary>
/// Describes a route as a named set of reference phrases, with optional metadata and an optional
/// per-route distance threshold that overrides the router default when classifying inputs.
/// </summary>
public sealed class Route
{
    /// <summary>
    /// Initializes a new <see cref="Route" />.
    /// </summary>
    /// <param name="name">The route name. Must be non-empty and must not contain a comma.</param>
    /// <param name="references">One or more non-empty reference phrases that exemplify the route.</param>
    /// <param name="metadata">Optional metadata stored alongside the route's references.</param>
    /// <param name="distanceThreshold">
    /// Optional per-route distance threshold. When set, a route only matches if its aggregated distance is
    /// at or below this value. Capped by the router's <see cref="SemanticRouterOptions.DistanceThreshold" />,
    /// which is the maximum routing radius. When <see langword="null" /> the router default is used.
    /// </param>
    public Route(
        string name,
        IEnumerable<string> references,
        IReadOnlyDictionary<string, object?>? metadata = null,
        double? distanceThreshold = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(references);

        if (name.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException("Route name must not contain a comma.", nameof(name));
        }

        var normalizedReferences = new List<string>();
        foreach (var reference in references)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(reference, nameof(references));
            normalizedReferences.Add(reference);
        }

        if (normalizedReferences.Count == 0)
        {
            throw new ArgumentException("A route must declare at least one reference.", nameof(references));
        }

        if (distanceThreshold is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), distanceThreshold, "Route distance threshold must be greater than zero.");
        }

        Name = name.Trim();
        References = normalizedReferences;
        Metadata = metadata;
        DistanceThreshold = distanceThreshold;
    }

    /// <summary>Gets the route name.</summary>
    public string Name { get; }

    /// <summary>Gets the reference phrases that exemplify the route.</summary>
    public IReadOnlyList<string> References { get; }

    /// <summary>Gets the optional metadata stored alongside the route's references.</summary>
    public IReadOnlyDictionary<string, object?>? Metadata { get; }

    /// <summary>Gets the optional per-route distance threshold, or <see langword="null" /> to use the router default.</summary>
    public double? DistanceThreshold { get; }
}

/// <summary>A single stored reference of a route.</summary>
public sealed record RouteReference(string Key, string RouteName, string Reference);
