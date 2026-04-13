namespace RedisVlDotNet.Schema;

public sealed record TagFieldDefinition : FieldDefinition
{
    public TagFieldDefinition(
        string name,
        string? alias = null,
        bool sortable = false,
        char separator = ',',
        bool caseSensitive = false,
        bool withSuffixTrie = false,
        bool indexMissing = false,
        bool indexEmpty = false,
        bool noIndex = false) : base(name, alias, sortable)
    {
        if (separator == default || char.IsWhiteSpace(separator))
        {
            throw new ArgumentException("Tag field separator must be a single non-whitespace character.", nameof(separator));
        }

        if (noIndex && !sortable)
        {
            throw new ArgumentException("NOINDEX fields must also be sortable so they remain queryable via sorting.", nameof(noIndex));
        }

        Separator = separator;
        CaseSensitive = caseSensitive;
        WithSuffixTrie = withSuffixTrie;
        IndexMissing = indexMissing;
        IndexEmpty = indexEmpty;
        NoIndex = noIndex;
    }

    public char Separator { get; }

    public bool CaseSensitive { get; }

    public bool WithSuffixTrie { get; }

    public bool IndexMissing { get; }

    public bool IndexEmpty { get; }

    public bool NoIndex { get; }
}
