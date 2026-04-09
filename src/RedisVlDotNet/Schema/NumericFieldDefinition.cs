namespace RedisVlDotNet.Schema;

public sealed record NumericFieldDefinition : FieldDefinition
{
    public NumericFieldDefinition(string name, string? alias = null, bool sortable = false)
        : base(name, alias, sortable)
    {
    }
}
