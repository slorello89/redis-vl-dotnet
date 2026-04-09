using System.Collections.ObjectModel;

namespace RedisVlDotNet.Schema;

public sealed record SearchSchema
{
    public SearchSchema(IndexDefinition index, IEnumerable<FieldDefinition> fields)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(fields);

        Index = index;
        Fields = new ReadOnlyCollection<FieldDefinition>(fields.ToList());
    }

    public IndexDefinition Index { get; }

    public IReadOnlyList<FieldDefinition> Fields { get; }
}
