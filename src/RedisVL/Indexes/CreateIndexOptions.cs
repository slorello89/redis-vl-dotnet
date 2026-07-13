namespace RedisVL.Indexes;

/// <summary>Controls how <see cref="SearchIndex.CreateAsync"/> behaves when the index already exists.</summary>
public sealed record CreateIndexOptions
{
    /// <summary>Initializes a new instance of the <see cref="CreateIndexOptions"/> record.</summary>
    /// <param name="skipIfExists">When <see langword="true"/>, creation is skipped if the index already exists.</param>
    /// <param name="overwrite">When <see langword="true"/>, an existing index is dropped and recreated.</param>
    /// <param name="dropExistingDocuments">When <see langword="true"/> and <paramref name="overwrite"/> is set, the underlying documents are deleted along with the index.</param>
    /// <exception cref="ArgumentException">Thrown when both <paramref name="skipIfExists"/> and <paramref name="overwrite"/> are enabled.</exception>
    public CreateIndexOptions(bool skipIfExists = false, bool overwrite = false, bool dropExistingDocuments = false)
    {
        if (skipIfExists && overwrite)
        {
            throw new ArgumentException("SkipIfExists and Overwrite cannot both be enabled.");
        }

        SkipIfExists = skipIfExists;
        Overwrite = overwrite;
        DropExistingDocuments = dropExistingDocuments;
    }

    /// <summary>Gets a value indicating whether index creation is skipped when the index already exists.</summary>
    public bool SkipIfExists { get; }

    /// <summary>Gets a value indicating whether an existing index is dropped and recreated.</summary>
    public bool Overwrite { get; }

    /// <summary>Gets a value indicating whether the underlying documents are deleted when an existing index is overwritten.</summary>
    public bool DropExistingDocuments { get; }
}
