namespace RedisVlDotNet.Schema;

public sealed record GeoFieldDefinition : FieldDefinition
{
    public GeoFieldDefinition(string name, string? alias = null, bool sortable = false)
        : base(name, alias, sortable)
    {
    }
}
