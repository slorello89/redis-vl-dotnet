namespace RedisVl.Schema;

public abstract record FieldDefinition
{
    protected FieldDefinition(string name, string? alias = null, bool sortable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Alias = alias;
        Sortable = sortable;
    }

    public string Name { get; }

    public string? Alias { get; }

    public bool Sortable { get; }
}
