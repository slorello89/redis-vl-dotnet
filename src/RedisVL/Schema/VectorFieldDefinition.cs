namespace RedisVL.Schema;

/// <summary>
/// Defines a <c>VECTOR</c> field for an index, enabling vector similarity search using the
/// algorithm and tuning parameters carried in its <see cref="VectorFieldAttributes"/>.
/// </summary>
public sealed record VectorFieldDefinition : FieldDefinition
{
    /// <summary>
    /// Initializes a new <see cref="VectorFieldDefinition"/>.
    /// </summary>
    /// <param name="name">The name of the source hash field or JSON path.</param>
    /// <param name="attributes">The vector algorithm and tuning parameters.</param>
    /// <param name="alias">The optional query alias (<c>AS</c>) exposed to searches.</param>
    /// <param name="indexMissing">When <see langword="true"/>, emits <c>INDEXMISSING</c> so missing values are queryable.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="attributes"/> is <see langword="null"/>.</exception>
    public VectorFieldDefinition(
        string name,
        VectorFieldAttributes attributes,
        string? alias = null,
        bool indexMissing = false) : base(name, alias)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        Attributes = attributes;
        IndexMissing = indexMissing;
    }

    /// <summary>The vector algorithm and tuning parameters for this field.</summary>
    public VectorFieldAttributes Attributes { get; }

    /// <summary>Whether documents missing this field are indexed (<c>INDEXMISSING</c>).</summary>
    public bool IndexMissing { get; }
}
