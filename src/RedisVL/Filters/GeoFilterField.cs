namespace RedisVL.Filters;

/// <summary>
/// Builds geospatial filters over a <c>GEO</c> field, matching points within a radius.
/// </summary>
public sealed class GeoFilterField
{
    private readonly string _fieldName;

    internal GeoFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    /// <summary>Matches points within <paramref name="radius"/> of the given center coordinate.</summary>
    /// <param name="longitude">The center longitude.</param>
    /// <param name="latitude">The center latitude.</param>
    /// <param name="radius">The search radius; must be greater than zero.</param>
    /// <param name="unit">The unit in which <paramref name="radius"/> is expressed.</param>
    /// <returns>A <see cref="FilterExpression"/> for the radius query.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is not greater than zero.</exception>
    public FilterExpression WithinRadius(double longitude, double latitude, double radius, GeoUnit unit)
    {
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Geo filter radius must be greater than zero.");
        }

        return new GeoFilterExpression(_fieldName, longitude, latitude, radius, unit);
    }
}
