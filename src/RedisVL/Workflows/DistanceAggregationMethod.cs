namespace RedisVL.Workflows;

/// <summary>
/// Determines how the vector distances of a route's multiple references are combined into a single
/// distance when classifying an input with <see cref="SemanticRouter.RouteManyAsync(string, float[], int?, DistanceAggregationMethod?, System.Threading.CancellationToken)" />.
/// </summary>
public enum DistanceAggregationMethod
{
    /// <summary>Use the average of the matched reference distances. This is the default.</summary>
    Average,

    /// <summary>Use the smallest (nearest) of the matched reference distances.</summary>
    Minimum,

    /// <summary>Use the sum of the matched reference distances.</summary>
    Sum
}
