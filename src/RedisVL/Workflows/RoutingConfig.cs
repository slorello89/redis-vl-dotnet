namespace RedisVL.Workflows;

/// <summary>
/// Configures how <see cref="SemanticRouter" /> classifies an input across routes: how many matches to
/// return and how a route's multiple reference distances are aggregated.
/// </summary>
public sealed class RoutingConfig
{
    /// <summary>
    /// Initializes a new <see cref="RoutingConfig" />.
    /// </summary>
    /// <param name="maxResults">The maximum number of routes to return from a multi-match call. Defaults to 1.</param>
    /// <param name="aggregationMethod">How a route's reference distances are aggregated. Defaults to <see cref="DistanceAggregationMethod.Average" />.</param>
    /// <param name="maxReferenceCandidates">
    /// The maximum number of nearest reference documents the underlying range query considers before
    /// aggregation. References beyond this cap (ordered nearest-first) are ignored, which can affect
    /// <see cref="DistanceAggregationMethod.Average" /> and <see cref="DistanceAggregationMethod.Sum" />
    /// when a route has very many references. Defaults to 1000.
    /// </param>
    public RoutingConfig(
        int maxResults = 1,
        DistanceAggregationMethod aggregationMethod = DistanceAggregationMethod.Average,
        int maxReferenceCandidates = 1000)
    {
        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), maxResults, "Routing max results must be greater than zero.");
        }

        if (maxReferenceCandidates <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReferenceCandidates), maxReferenceCandidates, "Routing max reference candidates must be greater than zero.");
        }

        MaxResults = maxResults;
        AggregationMethod = aggregationMethod;
        MaxReferenceCandidates = maxReferenceCandidates;
    }

    /// <summary>Gets the maximum number of routes returned from a multi-match call.</summary>
    public int MaxResults { get; }

    /// <summary>Gets the method used to aggregate a route's multiple reference distances.</summary>
    public DistanceAggregationMethod AggregationMethod { get; }

    /// <summary>Gets the maximum number of nearest reference documents considered before aggregation.</summary>
    public int MaxReferenceCandidates { get; }
}
