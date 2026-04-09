namespace RedisVlDotNet.Schema;

public sealed record VectorFieldDefinition : FieldDefinition
{
    public VectorFieldDefinition(
        string name,
        VectorFieldAttributes attributes,
        string? alias = null) : base(name, alias)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        Attributes = attributes;
    }

    public VectorFieldAttributes Attributes { get; }
}
