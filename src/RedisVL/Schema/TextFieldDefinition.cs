namespace RedisVL.Schema;

/// <summary>
/// Defines a full-text (<c>TEXT</c>) field for an index, supporting tokenization, stemming,
/// phonetic matching, and relevance weighting.
/// </summary>
public sealed record TextFieldDefinition : FieldDefinition
{
    /// <summary>
    /// Initializes a new <see cref="TextFieldDefinition"/>.
    /// </summary>
    /// <param name="name">The name of the source hash field or JSON path.</param>
    /// <param name="alias">The optional query alias (<c>AS</c>) exposed to searches.</param>
    /// <param name="sortable">Whether the field is <c>SORTABLE</c>.</param>
    /// <param name="noStem">When <see langword="true"/>, emits <c>NOSTEM</c> to disable stemming.</param>
    /// <param name="phoneticMatch">When <see langword="true"/>, enables phonetic matching (<c>PHONETIC</c>).</param>
    /// <param name="weight">The relevance <c>WEIGHT</c>; must be greater than zero.</param>
    /// <param name="withSuffixTrie">When <see langword="true"/>, emits <c>WITHSUFFIXTRIE</c> for suffix and infix matching.</param>
    /// <param name="indexMissing">When <see langword="true"/>, emits <c>INDEXMISSING</c> so missing values are queryable.</param>
    /// <param name="indexEmpty">When <see langword="true"/>, emits <c>INDEXEMPTY</c> so empty values are queryable.</param>
    /// <param name="noIndex">When <see langword="true"/>, emits <c>NOINDEX</c>; requires <paramref name="sortable"/> to remain queryable.</param>
    /// <param name="unNormalizedForm">When <see langword="true"/>, emits <c>UNF</c> to keep the original casing for sorting; requires <paramref name="sortable"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="weight"/> is not greater than zero.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="noIndex"/> or <paramref name="unNormalizedForm"/> is set without <paramref name="sortable"/>.</exception>
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

    /// <summary>Whether stemming is disabled (<c>NOSTEM</c>).</summary>
    public bool NoStem { get; }

    /// <summary>Whether phonetic matching is enabled (<c>PHONETIC</c>).</summary>
    public bool PhoneticMatch { get; }

    /// <summary>The relevance <c>WEIGHT</c> applied to matches on this field.</summary>
    public double Weight { get; }

    /// <summary>Whether a suffix trie is built (<c>WITHSUFFIXTRIE</c>) for suffix and infix matching.</summary>
    public bool WithSuffixTrie { get; }

    /// <summary>Whether documents missing this field are indexed (<c>INDEXMISSING</c>).</summary>
    public bool IndexMissing { get; }

    /// <summary>Whether empty values for this field are indexed (<c>INDEXEMPTY</c>).</summary>
    public bool IndexEmpty { get; }

    /// <summary>Whether the field is excluded from the inverted index (<c>NOINDEX</c>).</summary>
    public bool NoIndex { get; }

    /// <summary>Whether the original, un-normalized form is preserved for sorting (<c>UNF</c>).</summary>
    public bool UnNormalizedForm { get; }
}
