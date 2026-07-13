namespace RedisVL.Schema;

/// <summary>
/// Defines a <c>NUMERIC</c> field for an index, enabling numeric range filtering and sorting.
/// </summary>
public sealed record NumericFieldDefinition : FieldDefinition
{
    /// <summary>
    /// Initializes a new <see cref="NumericFieldDefinition"/>.
    /// </summary>
    /// <param name="name">The name of the source hash field or JSON path.</param>
    /// <param name="alias">The optional query alias (<c>AS</c>) exposed to searches.</param>
    /// <param name="sortable">Whether the field is <c>SORTABLE</c>.</param>
    /// <param name="indexMissing">When <see langword="true"/>, emits <c>INDEXMISSING</c> so missing values are queryable.</param>
    /// <param name="noIndex">When <see langword="true"/>, emits <c>NOINDEX</c>; requires <paramref name="sortable"/> to remain queryable.</param>
    /// <param name="unNormalizedForm">When <see langword="true"/>, emits <c>UNF</c> to preserve the original value for sorting; requires <paramref name="sortable"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="noIndex"/> or <paramref name="unNormalizedForm"/> is set without <paramref name="sortable"/>.</exception>
    public NumericFieldDefinition(
        string name,
        string? alias = null,
        bool sortable = false,
        bool indexMissing = false,
        bool noIndex = false,
        bool unNormalizedForm = false)
        : base(name, alias, sortable)
    {
        if (noIndex && !sortable)
        {
            throw new ArgumentException("NOINDEX fields must also be sortable so they remain queryable via sorting.", nameof(noIndex));
        }

        if (unNormalizedForm && !sortable)
        {
            throw new ArgumentException("UNF can only be enabled for sortable fields.", nameof(unNormalizedForm));
        }

        IndexMissing = indexMissing;
        NoIndex = noIndex;
        UnNormalizedForm = unNormalizedForm;
    }

    /// <summary>Whether documents missing this field are indexed (<c>INDEXMISSING</c>).</summary>
    public bool IndexMissing { get; }

    /// <summary>Whether the field is excluded from the inverted index (<c>NOINDEX</c>).</summary>
    public bool NoIndex { get; }

    /// <summary>Whether the original, un-normalized value is preserved for sorting (<c>UNF</c>).</summary>
    public bool UnNormalizedForm { get; }
}
