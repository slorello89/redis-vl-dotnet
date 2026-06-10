namespace RedisVL.Filters;

public sealed class GeoFilterField
{
    private readonly string _fieldName;

    internal GeoFilterField(string fieldName)
    {
        _fieldName = FilterExpression.NormalizeFieldName(fieldName);
    }

    public FilterExpression WithinRadius(double longitude, double latitude, double radius, GeoUnit unit)
    {
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Geo filter radius must be greater than zero.");
        }

        return new GeoFilterExpression(_fieldName, longitude, latitude, radius, unit);
    }

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
