using System.Collections.ObjectModel;
using RedisVL.Schema;

namespace RedisVL.Caches;

public sealed class SemanticCacheOptions
{
    public SemanticCacheOptions(
        string name,
        VectorFieldAttributes embeddingFieldAttributes,
        double distanceThreshold,
        string? keyNamespace = null,
        TimeSpan? timeToLive = null,
        string promptFieldName = "prompt",
        string responseFieldName = "response",
        string metadataFieldName = "metadata",
        string embeddingFieldName = "embedding",
        IEnumerable<FieldDefinition>? filterableFields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(embeddingFieldAttributes);
        ArgumentException.ThrowIfNullOrWhiteSpace(promptFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(responseFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(embeddingFieldName);

        if (distanceThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distanceThreshold), distanceThreshold, "Semantic cache distance threshold must be greater than zero.");
        }

        if (timeToLive.HasValue && timeToLive.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeToLive), "Semantic cache TTL must be positive when provided.");
        }

        if (embeddingFieldAttributes.DataType != VectorDataType.Float32)
        {
            throw new ArgumentException("Semantic cache currently supports only FLOAT32 embeddings.", nameof(embeddingFieldAttributes));
        }

        Name = name.Trim();
        EmbeddingFieldAttributes = embeddingFieldAttributes;
        DistanceThreshold = distanceThreshold;
        KeyNamespace = string.IsNullOrWhiteSpace(keyNamespace) ? null : keyNamespace.Trim();
        TimeToLive = timeToLive;
        PromptFieldName = promptFieldName.Trim();
        ResponseFieldName = responseFieldName.Trim();
        MetadataFieldName = metadataFieldName.Trim();
        EmbeddingFieldName = embeddingFieldName.Trim();
        FilterableFields = NormalizeFilterableFields(filterableFields);
    }

    public string Name { get; }

    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    public double DistanceThreshold { get; }

    public string? KeyNamespace { get; }

    public TimeSpan? TimeToLive { get; }

    public string PromptFieldName { get; }

    public string ResponseFieldName { get; }

    public string MetadataFieldName { get; }

    public string EmbeddingFieldName { get; }

    public IReadOnlyList<FieldDefinition> FilterableFields { get; }

    private ReadOnlyCollection<FieldDefinition> NormalizeFilterableFields(IEnumerable<FieldDefinition>? filterableFields)
    {
        var reservedFieldNames = new HashSet<string>(StringComparer.Ordinal)
        {
            PromptFieldName,
            ResponseFieldName,
            MetadataFieldName,
            EmbeddingFieldName
        };

        var normalizedFields = new List<FieldDefinition>();
        if (filterableFields is null)
        {
            return new ReadOnlyCollection<FieldDefinition>(normalizedFields);
        }

        foreach (var field in filterableFields)
        {
            ArgumentNullException.ThrowIfNull(field);

            if (field.Alias is not null)
            {
                throw new ArgumentException("Semantic cache filterable fields cannot define aliases.", nameof(filterableFields));
            }

            if (field is not TagFieldDefinition and not TextFieldDefinition and not NumericFieldDefinition)
            {
                throw new ArgumentException("Semantic cache filterable fields must use TAG, TEXT, or NUMERIC schema definitions.", nameof(filterableFields));
            }

            if (!reservedFieldNames.Add(field.Name))
            {
                throw new ArgumentException($"Semantic cache field '{field.Name}' conflicts with an existing cache field.", nameof(filterableFields));
            }

            normalizedFields.Add(field);
        }

        return new ReadOnlyCollection<FieldDefinition>(normalizedFields);
    }
}
