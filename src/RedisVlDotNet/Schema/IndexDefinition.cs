using System.Collections.ObjectModel;

namespace RedisVlDotNet.Schema;

public sealed record IndexDefinition
{
    public IndexDefinition(string name, string prefix, StorageType storageType)
        : this(name, [prefix], storageType)
    {
    }

    public IndexDefinition(string name, IEnumerable<string> prefixes, StorageType storageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(prefixes);

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
    }

    public string Name { get; }

    public string Prefix { get; }

    public IReadOnlyList<string> Prefixes { get; }

    public StorageType StorageType { get; }
}
