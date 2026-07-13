namespace RedisVL.Schema;

/// <summary>
/// Defines a <c>GEO</c> field for an index, storing longitude/latitude pairs for geospatial
/// radius and bounding-box filtering.
/// </summary>
public sealed record GeoFieldDefinition : FieldDefinition
{
    /// <summary>
    /// Initializes a new <see cref="GeoFieldDefinition"/>.
    /// </summary>
    /// <param name="name">The name of the source hash field or JSON path.</param>
    /// <param name="alias">The optional query alias (<c>AS</c>) exposed to searches.</param>
    /// <param name="sortable">Whether the field is <c>SORTABLE</c>.</param>
    /// <param name="indexMissing">When <see langword="true"/>, emits <c>INDEXMISSING</c> so missing values are queryable.</param>
    /// <param name="noIndex">When <see langword="true"/>, emits <c>NOINDEX</c>; requires <paramref name="sortable"/> to remain queryable.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="noIndex"/> is set without <paramref name="sortable"/>.</exception>
    public GeoFieldDefinition(
        string name,
        string? alias = null,
        bool sortable = false,
        bool indexMissing = false,
        bool noIndex = false)
        : base(name, alias, sortable)
    {
        if (noIndex && !sortable)
        {
            throw new ArgumentException("NOINDEX fields must also be sortable so they remain queryable via sorting.", nameof(noIndex));
        }

        IndexMissing = indexMissing;
        NoIndex = noIndex;
    }

    /// <summary>Whether documents missing this field are indexed (<c>INDEXMISSING</c>).</summary>
    public bool IndexMissing { get; }

    /// <summary>Whether the field is excluded from the inverted index (<c>NOINDEX</c>).</summary>
    public bool NoIndex { get; }
}
