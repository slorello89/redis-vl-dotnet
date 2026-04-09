namespace RedisVlDotNet.Schema;

public sealed record TextFieldDefinition : FieldDefinition
{
    public TextFieldDefinition(
        string name,
        string? alias = null,
        bool sortable = false,
        bool noStem = false,
        bool phoneticMatch = false) : base(name, alias, sortable)
    {
        NoStem = noStem;
        PhoneticMatch = phoneticMatch;
    }

    public bool NoStem { get; }

    public bool PhoneticMatch { get; }
}
