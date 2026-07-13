namespace RedisVL.Filters;

/// <summary>The distance unit used by geospatial radius filters.</summary>
public enum GeoUnit
{
    /// <summary>Meters (<c>m</c>).</summary>
    Meters,

    /// <summary>Kilometers (<c>km</c>).</summary>
    Kilometers,

    /// <summary>Miles (<c>mi</c>).</summary>
    Miles,

    /// <summary>Feet (<c>ft</c>).</summary>
    Feet
}
