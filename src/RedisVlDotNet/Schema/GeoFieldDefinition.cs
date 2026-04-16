namespace RedisVl.Schema;

public sealed record GeoFieldDefinition : FieldDefinition
{
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

    public bool IndexMissing { get; }

    public bool NoIndex { get; }
}
