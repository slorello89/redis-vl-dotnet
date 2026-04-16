namespace RedisVl.Indexes;

public sealed record CreateIndexOptions
{
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

    public bool SkipIfExists { get; }

    public bool Overwrite { get; }

    public bool DropExistingDocuments { get; }
}
