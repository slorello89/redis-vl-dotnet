using System.Collections.ObjectModel;

namespace RedisVL.Schema;

/// <summary>
/// Describes the top-level settings of a search index used to build the <c>FT.CREATE</c> command,
/// including its name, key prefixes, storage type, and indexing options.
/// </summary>
public sealed record IndexDefinition
{
    /// <summary>
    /// Initializes a new <see cref="IndexDefinition"/> that indexes keys under a single <paramref name="prefix"/>.
    /// </summary>
    /// <param name="name">The index name passed to <c>FT.CREATE</c>.</param>
    /// <param name="prefix">The key prefix whose keys are indexed.</param>
    /// <param name="storageType">Whether indexed keys are stored as Redis hashes or JSON documents.</param>
    /// <param name="keySeparator">The single non-whitespace character separating the prefix from the key id.</param>
    /// <param name="stopwords">The custom stopword list, or <see langword="null"/> to use the server default.</param>
    /// <param name="maxTextFields">When <see langword="true"/>, emits <c>MAXTEXTFIELDS</c> to allow more than 32 text fields.</param>
    /// <param name="temporarySeconds">When greater than zero, creates a temporary index (<c>TEMPORARY</c>) that expires after this many seconds of inactivity.</param>
    /// <param name="noOffsets">When <see langword="true"/>, emits <c>NOOFFSETS</c> to disable term offset storage.</param>
    /// <param name="noHighlight">When <see langword="true"/>, emits <c>NOHL</c> to disable highlighting support.</param>
    /// <param name="noFields">When <see langword="true"/>, emits <c>NOFIELDS</c> to disable field bit storage.</param>
    /// <param name="noFrequencies">When <see langword="true"/>, emits <c>NOFREQS</c> to disable term frequency storage.</param>
    /// <param name="skipInitialScan">When <see langword="true"/>, emits <c>SKIPINITIALSCAN</c> to skip indexing pre-existing keys.</param>
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

    /// <summary>
    /// Initializes a new <see cref="IndexDefinition"/> that indexes keys under one or more <paramref name="prefixes"/>.
    /// </summary>
    /// <param name="name">The index name passed to <c>FT.CREATE</c>.</param>
    /// <param name="prefixes">The key prefixes whose keys are indexed; at least one non-blank value is required.</param>
    /// <param name="storageType">Whether indexed keys are stored as Redis hashes or JSON documents.</param>
    /// <param name="keySeparator">The single non-whitespace character separating the prefix from the key id.</param>
    /// <param name="stopwords">The custom stopword list, or <see langword="null"/> to use the server default.</param>
    /// <param name="maxTextFields">When <see langword="true"/>, emits <c>MAXTEXTFIELDS</c> to allow more than 32 text fields.</param>
    /// <param name="temporarySeconds">When greater than zero, creates a temporary index (<c>TEMPORARY</c>) that expires after this many seconds of inactivity.</param>
    /// <param name="noOffsets">When <see langword="true"/>, emits <c>NOOFFSETS</c> to disable term offset storage.</param>
    /// <param name="noHighlight">When <see langword="true"/>, emits <c>NOHL</c> to disable highlighting support.</param>
    /// <param name="noFields">When <see langword="true"/>, emits <c>NOFIELDS</c> to disable field bit storage.</param>
    /// <param name="noFrequencies">When <see langword="true"/>, emits <c>NOFREQS</c> to disable term frequency storage.</param>
    /// <param name="skipInitialScan">When <see langword="true"/>, emits <c>SKIPINITIALSCAN</c> to skip indexing pre-existing keys.</param>
    /// <exception cref="ArgumentException">Thrown when the name is blank, the key separator is invalid, or prefixes contain blank values or are empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="temporarySeconds"/> is negative.</exception>
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

    /// <summary>The index name passed to <c>FT.CREATE</c>.</summary>
    public string Name { get; }

    /// <summary>The primary (first) key prefix indexed by this definition.</summary>
    public string Prefix { get; }

    /// <summary>All key prefixes indexed by this definition.</summary>
    public IReadOnlyList<string> Prefixes { get; }

    /// <summary>Whether indexed keys are stored as Redis hashes or JSON documents.</summary>
    public StorageType StorageType { get; }

    /// <summary>The single character separating the prefix from the key id.</summary>
    public char KeySeparator { get; }

    /// <summary>The custom stopword list, or <see langword="null"/> when the server default is used.</summary>
    public IReadOnlyList<string>? Stopwords { get; }

    /// <summary>Whether <c>MAXTEXTFIELDS</c> is set, allowing more than 32 text fields.</summary>
    public bool MaxTextFields { get; }

    /// <summary>The <c>TEMPORARY</c> expiration in seconds; zero for a permanent index.</summary>
    public int TemporarySeconds { get; }

    /// <summary>Whether <c>NOOFFSETS</c> is set, disabling term offset storage.</summary>
    public bool NoOffsets { get; }

    /// <summary>Whether <c>NOHL</c> is set, disabling highlighting support.</summary>
    public bool NoHighlight { get; }

    /// <summary>Whether <c>NOFIELDS</c> is set, disabling field bit storage.</summary>
    public bool NoFields { get; }

    /// <summary>Whether <c>NOFREQS</c> is set, disabling term frequency storage.</summary>
    public bool NoFrequencies { get; }

    /// <summary>Whether <c>SKIPINITIALSCAN</c> is set, skipping indexing of pre-existing keys.</summary>
    public bool SkipInitialScan { get; }
}
