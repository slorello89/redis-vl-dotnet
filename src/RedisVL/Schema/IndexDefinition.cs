using System.Collections.ObjectModel;

namespace RedisVL.Schema;

public sealed record IndexDefinition
{
    public IndexDefinition(
        string name,
        string prefix,
        StorageType storageType,
        char keySeparator = ':',
        IEnumerable<string>? stopwords = null,
        bool maxTextFields = false,
        int temporarySeconds = 0,
        bool noOffsets = false,
        bool noHighlight = false,
        bool noFields = false,
        bool noFrequencies = false,
        bool skipInitialScan = false)
        : this(name, [prefix], storageType, keySeparator, stopwords, maxTextFields, temporarySeconds, noOffsets, noHighlight, noFields, noFrequencies, skipInitialScan)
    {
    }

    public IndexDefinition(
        string name,
        IEnumerable<string> prefixes,
        StorageType storageType,
        char keySeparator = ':',
        IEnumerable<string>? stopwords = null,
        bool maxTextFields = false,
        int temporarySeconds = 0,
        bool noOffsets = false,
        bool noHighlight = false,
        bool noFields = false,
        bool noFrequencies = false,
        bool skipInitialScan = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(prefixes);
        if (keySeparator == default || char.IsWhiteSpace(keySeparator))
        {
            throw new ArgumentException("Index key separator must be a single non-whitespace character.", nameof(keySeparator));
        }

        if (temporarySeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(temporarySeconds), temporarySeconds, "Temporary index expiration must be zero or greater.");
        }

        Name = name;
        Prefixes = new ReadOnlyCollection<string>(
            prefixes
                .Select(static prefix => string.IsNullOrWhiteSpace(prefix)
                    ? throw new ArgumentException("Index prefixes cannot contain blank values.", nameof(prefixes))
                    : prefix.Trim())
                .ToList());
        if (Prefixes.Count == 0)
        {
            throw new ArgumentException("Index prefixes must include at least one value.", nameof(prefixes));
        }

        Prefix = Prefixes[0];
        StorageType = storageType;
        KeySeparator = keySeparator;
        Stopwords = stopwords is null
            ? null
            : new ReadOnlyCollection<string>(
                stopwords
                    .Select(static stopword => string.IsNullOrWhiteSpace(stopword)
                        ? throw new ArgumentException("Index stopwords cannot contain blank values.", nameof(stopwords))
                        : stopword.Trim())
                    .ToList());
        MaxTextFields = maxTextFields;
        TemporarySeconds = temporarySeconds;
        NoOffsets = noOffsets;
        NoHighlight = noHighlight;
        NoFields = noFields;
        NoFrequencies = noFrequencies;
        SkipInitialScan = skipInitialScan;
    }

    public string Name { get; }

    public string Prefix { get; }

    public IReadOnlyList<string> Prefixes { get; }

    public StorageType StorageType { get; }

    public char KeySeparator { get; }

    public IReadOnlyList<string>? Stopwords { get; }

    public bool MaxTextFields { get; }

    public int TemporarySeconds { get; }

    public bool NoOffsets { get; }

    public bool NoHighlight { get; }

    public bool NoFields { get; }

    public bool NoFrequencies { get; }

    public bool SkipInitialScan { get; }
}
