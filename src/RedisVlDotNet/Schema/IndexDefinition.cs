using System.Collections.ObjectModel;

namespace RedisVlDotNet.Schema;

public sealed record IndexDefinition
{
    public IndexDefinition(string name, string prefix, StorageType storageType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        Name = name;
        Prefix = prefix;
        StorageType = storageType;
    }

    public string Name { get; }

    public string Prefix { get; }

    public StorageType StorageType { get; }
}
