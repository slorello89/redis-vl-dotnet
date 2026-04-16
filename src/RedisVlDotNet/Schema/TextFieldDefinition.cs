namespace RedisVl.Schema;

public sealed record TextFieldDefinition : FieldDefinition
{
    public TextFieldDefinition(
        string name,
        string? alias = null,
        bool sortable = false,
        bool noStem = false,
        bool phoneticMatch = false,
        double weight = 1d,
        bool withSuffixTrie = false,
        bool indexMissing = false,
        bool indexEmpty = false,
        bool noIndex = false,
        bool unNormalizedForm = false) : base(name, alias, sortable)
    {
        if (weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), weight, "Text field weight must be greater than zero.");
        }

        if (noIndex && !sortable)
        {
            throw new ArgumentException("NOINDEX fields must also be sortable so they remain queryable via sorting.", nameof(noIndex));
        }

        if (unNormalizedForm && !sortable)
        {
            throw new ArgumentException("UNF can only be enabled for sortable fields.", nameof(unNormalizedForm));
        }

        NoStem = noStem;
        PhoneticMatch = phoneticMatch;
        Weight = weight;
        WithSuffixTrie = withSuffixTrie;
        IndexMissing = indexMissing;
        IndexEmpty = indexEmpty;
        NoIndex = noIndex;
        UnNormalizedForm = unNormalizedForm;
    }

    public bool NoStem { get; }

    public bool PhoneticMatch { get; }

    public double Weight { get; }

    public bool WithSuffixTrie { get; }

    public bool IndexMissing { get; }

    public bool IndexEmpty { get; }

    public bool NoIndex { get; }

    public bool UnNormalizedForm { get; }
}
