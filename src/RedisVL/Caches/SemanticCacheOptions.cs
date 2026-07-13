using System.Collections.ObjectModel;
using RedisVL.Schema;

namespace RedisVL.Caches;

/// <summary>Configuration for a <see cref="SemanticCache" />, including its schema, matching threshold, and field names.</summary>
public sealed class SemanticCacheOptions
{
    /// <summary>Initializes a new <see cref="SemanticCacheOptions" />.</summary>
    /// <param name="name">The cache name, used as part of the index name and key prefix.</param>
    /// <param name="embeddingFieldAttributes">The vector field attributes (dimensions, distance metric, etc.) for the embedding field; must use <see cref="VectorDataType.Float32" />.</param>
    /// <param name="distanceThreshold">The maximum vector distance for a cached entry to be considered a match; must be greater than zero.</param>
    /// <param name="keyNamespace">An optional namespace inserted into the index name and key prefix to partition entries.</param>
    /// <param name="timeToLive">An optional default expiry applied to stored entries; must be positive when provided.</param>
    /// <param name="promptFieldName">The hash field name used to store the prompt text.</param>
    /// <param name="responseFieldName">The hash field name used to store the response text.</param>
    /// <param name="metadataFieldName">The hash field name used to store serialized metadata.</param>
    /// <param name="embeddingFieldName">The hash field name used to store the embedding vector.</param>
    /// <param name="filterableFields">Optional additional indexed fields that can be used to filter cache lookups.</param>
    /// <param name="trackStatistics">Whether the cache should track hit/miss statistics.</param>
    /// <exception cref="ArgumentException">A required name argument is blank, or <paramref name="embeddingFieldAttributes" /> is not <see cref="VectorDataType.Float32" />.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="embeddingFieldAttributes" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="distanceThreshold" /> is not positive, or <paramref name="timeToLive" /> is zero or negative.</exception>
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
        IEnumerable<FieldDefinition>? filterableFields = null,
        bool trackStatistics = false)
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
        TrackStatistics = trackStatistics;
    }

    /// <summary>Gets the cache name used as part of the index name and key prefix.</summary>
    public string Name { get; }

    /// <summary>Gets the vector field attributes for the embedding field.</summary>
    public VectorFieldAttributes EmbeddingFieldAttributes { get; }

    /// <summary>Gets the maximum vector distance for a cached entry to be considered a match.</summary>
    public double DistanceThreshold { get; }

    /// <summary>Gets the optional namespace inserted into the index name and key prefix, or <see langword="null" /> when unset.</summary>
    public string? KeyNamespace { get; }

    /// <summary>Gets the optional default expiry applied to stored entries, or <see langword="null" /> for no expiry.</summary>
    public TimeSpan? TimeToLive { get; }

    /// <summary>Gets the hash field name used to store the prompt text.</summary>
    public string PromptFieldName { get; }

    /// <summary>Gets the hash field name used to store the response text.</summary>
    public string ResponseFieldName { get; }

    /// <summary>Gets the hash field name used to store serialized metadata.</summary>
    public string MetadataFieldName { get; }

    /// <summary>Gets the hash field name used to store the embedding vector.</summary>
    public string EmbeddingFieldName { get; }

    /// <summary>Gets the additional indexed fields that can be used to filter cache lookups.</summary>
    public IReadOnlyList<FieldDefinition> FilterableFields { get; }

    /// <summary>
    /// Gets a value indicating whether the cache tracks hit/miss statistics. When <see langword="false" />
    /// (the default), <see cref="SemanticCache.HitCount" />, <see cref="SemanticCache.MissCount" />, and
    /// <see cref="SemanticCache.HitRate" /> stay at zero.
    /// </summary>
    public bool TrackStatistics { get; }

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
