namespace RedisVL.Filters;

/// <summary>
/// Builds geospatial filters over a <c>GEO</c> field, matching points within a radius or a
/// bounding box.
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

    /// <summary>Matches points inside the given longitude/latitude bounding box.</summary>
    /// <param name="minLongitude">The minimum (west) longitude.</param>
    /// <param name="minLatitude">The minimum (south) latitude.</param>
    /// <param name="maxLongitude">The maximum (east) longitude.</param>
    /// <param name="maxLatitude">The maximum (north) latitude.</param>
    /// <returns>A <see cref="FilterExpression"/> for the bounding-box query.</returns>
    /// <exception cref="ArgumentException">Thrown when a minimum bound exceeds its corresponding maximum bound.</exception>
    public FilterExpression WithinBox(double minLongitude, double minLatitude, double maxLongitude, double maxLatitude)
    {
        if (minLongitude > maxLongitude)
        {
            throw new ArgumentException("Geo box minimum longitude cannot be greater than maximum longitude.");
        }

        if (minLatitude > maxLatitude)
        {
            throw new ArgumentException("Geo box minimum latitude cannot be greater than maximum latitude.");
        }

        return new GeoBoxFilterExpression(_fieldName, minLongitude, minLatitude, maxLongitude, maxLatitude);
    }
}
