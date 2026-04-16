namespace RedisVL.Schema;

public sealed record VectorFieldDefinition : FieldDefinition
{
    public VectorFieldDefinition(
        string name,
        VectorFieldAttributes attributes,
        string? alias = null,
        bool indexMissing = false) : base(name, alias)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        Attributes = attributes;
        IndexMissing = indexMissing;
    }

    public VectorFieldAttributes Attributes { get; }

    public bool IndexMissing { get; }
}
