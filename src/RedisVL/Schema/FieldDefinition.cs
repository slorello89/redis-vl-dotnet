namespace RedisVL.Schema;

/// <summary>
/// The base type for all index field definitions, holding the properties common to every
/// field type such as its source name, query alias, and sortability.
/// </summary>
public abstract record FieldDefinition
{
    /// <summary>
    /// Initializes the shared state of a field definition.
    /// </summary>
    /// <param name="name">The name of the source hash field or JSON path.</param>
    /// <param name="alias">The optional query alias (<c>AS</c>) exposed to searches.</param>
    /// <param name="sortable">Whether the field is <c>SORTABLE</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is <see langword="null"/> or blank.</exception>
    protected FieldDefinition(string name, string? alias = null, bool sortable = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Alias = alias;
        Sortable = sortable;
    }

    /// <summary>The name of the source hash field or JSON path.</summary>
    public string Name { get; }

    /// <summary>The optional query alias (<c>AS</c>) exposed to searches, or <see langword="null"/>.</summary>
    public string? Alias { get; }

    /// <summary>Whether the field is <c>SORTABLE</c>.</summary>
    public bool Sortable { get; }
}
