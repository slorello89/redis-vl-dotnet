using System.Collections.ObjectModel;

namespace RedisVlDotNet.Schema;

public sealed record IndexDefinition
{
    public IndexDefinition(
        string name,
        string prefix,
        StorageType storageType,
        char keySeparator = ':',
        IEnumerable<string>? stopwords = null)
        : this(name, [prefix], storageType, keySeparator, stopwords)
    {
    }

    public IndexDefinition(
        string name,
        IEnumerable<string> prefixes,
        StorageType storageType,
        char keySeparator = ':',
        IEnumerable<string>? stopwords = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(prefixes);
        if (keySeparator == default || char.IsWhiteSpace(keySeparator))
        {
            throw new ArgumentException("Index key separator must be a single non-whitespace character.", nameof(keySeparator));
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
    }

    public string Name { get; }

    public string Prefix { get; }

    public IReadOnlyList<string> Prefixes { get; }

    public StorageType StorageType { get; }

    public char KeySeparator { get; }

    public IReadOnlyList<string>? Stopwords { get; }
}
