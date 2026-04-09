namespace RedisVlDotNet.Schema;

public sealed record TagFieldDefinition : FieldDefinition
{
    public TagFieldDefinition(
        string name,
        string? alias = null,
        bool sortable = false,
        char separator = ',',
        bool caseSensitive = false) : base(name, alias, sortable)
    {
        Separator = separator;
        CaseSensitive = caseSensitive;
    }

    public char Separator { get; }

    public bool CaseSensitive { get; }
}
