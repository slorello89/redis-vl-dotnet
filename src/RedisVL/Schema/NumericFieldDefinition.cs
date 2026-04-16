namespace RedisVL.Schema;

public sealed record NumericFieldDefinition : FieldDefinition
{
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

    public bool IndexMissing { get; }

    public bool NoIndex { get; }

    public bool UnNormalizedForm { get; }
}
