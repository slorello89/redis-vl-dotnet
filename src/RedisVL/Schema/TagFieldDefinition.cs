namespace RedisVL.Schema;

/// <summary>
/// Defines a <c>TAG</c> field for an index, storing delimited sets of exact-match tokens
/// such as categories or identifiers.
/// </summary>
public sealed record TagFieldDefinition : FieldDefinition
{
    /// <summary>
    /// Initializes a new <see cref="TagFieldDefinition"/>.
    /// </summary>
    /// <param name="name">The name of the source hash field or JSON path.</param>
    /// <param name="alias">The optional query alias (<c>AS</c>) exposed to searches.</param>
    /// <param name="sortable">Whether the field is <c>SORTABLE</c>.</param>
    /// <param name="separator">The single non-whitespace character separating tag values (<c>SEPARATOR</c>).</param>
    /// <param name="caseSensitive">When <see langword="true"/>, emits <c>CASESENSITIVE</c> to preserve tag casing.</param>
    /// <param name="withSuffixTrie">When <see langword="true"/>, emits <c>WITHSUFFIXTRIE</c> for suffix and infix matching.</param>
    /// <param name="indexMissing">When <see langword="true"/>, emits <c>INDEXMISSING</c> so missing values are queryable.</param>
    /// <param name="indexEmpty">When <see langword="true"/>, emits <c>INDEXEMPTY</c> so empty values are queryable.</param>
    /// <param name="noIndex">When <see langword="true"/>, emits <c>NOINDEX</c>; requires <paramref name="sortable"/> to remain queryable.</param>
    /// <exception cref="ArgumentException">Thrown when the separator is invalid, or when <paramref name="noIndex"/> is set without <paramref name="sortable"/>.</exception>
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

    /// <summary>The character separating individual tag values (<c>SEPARATOR</c>).</summary>
    public char Separator { get; }

    /// <summary>Whether tag matching is case-sensitive (<c>CASESENSITIVE</c>).</summary>
    public bool CaseSensitive { get; }

    /// <summary>Whether a suffix trie is built (<c>WITHSUFFIXTRIE</c>) for suffix and infix matching.</summary>
    public bool WithSuffixTrie { get; }

    /// <summary>Whether documents missing this field are indexed (<c>INDEXMISSING</c>).</summary>
    public bool IndexMissing { get; }

    /// <summary>Whether empty values for this field are indexed (<c>INDEXEMPTY</c>).</summary>
    public bool IndexEmpty { get; }

    /// <summary>Whether the field is excluded from the inverted index (<c>NOINDEX</c>).</summary>
    public bool NoIndex { get; }
}
