using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using RedisVL.Schema;

namespace RedisVL.Connectors.VectorData.Mapping;

internal enum RedisVLFieldKind
{
    Key,
    Tag,
    Text,
    Numeric,
    Vector,
    Unindexed
}

/// <summary>
/// A single mapped property of a vector-store record: the CLR property, the JSON/index
/// name it is stored under, and the Redis index field kind it maps to.
/// </summary>
internal sealed class RedisVLProperty
{
    public RedisVLProperty(PropertyInfo property, string jsonName, RedisVLFieldKind kind)
    {
        Property = property;
        JsonName = jsonName;
        Kind = kind;
    }

    public PropertyInfo Property { get; }

    public string JsonName { get; }

    public RedisVLFieldKind Kind { get; }

    // Vector-only metadata.
    public int Dimensions { get; init; }

    public VectorDistanceMetric Metric { get; init; }

    public VectorAlgorithm Algorithm { get; init; }

    public VectorDataType DataType { get; init; }
}

/// <summary>
/// Reflects a CLR record type (and/or a <see cref="VectorStoreCollectionDefinition"/>) into the
/// key / data / vector property model RedisVL needs to build a <see cref="SearchSchema"/> and
/// translate filters.
/// </summary>
internal sealed class RedisVLRecordModel
{
    private RedisVLRecordModel(
        RedisVLProperty key,
        IReadOnlyList<RedisVLProperty> data,
        IReadOnlyList<RedisVLProperty> vectors)
    {
        Key = key;
        Data = data;
        Vectors = vectors;

        var byJsonName = new Dictionary<string, RedisVLProperty>(StringComparer.Ordinal);
        var byClrName = new Dictionary<string, RedisVLProperty>(StringComparer.Ordinal);
        foreach (var property in new[] { key }.Concat(data).Concat(vectors))
        {
            byJsonName[property.JsonName] = property;
            byClrName[property.Property.Name] = property;
        }

        ByJsonName = byJsonName;
        ByClrName = byClrName;
    }

    public RedisVLProperty Key { get; }

    public IReadOnlyList<RedisVLProperty> Data { get; }

    public IReadOnlyList<RedisVLProperty> Vectors { get; }

    public IReadOnlyDictionary<string, RedisVLProperty> ByJsonName { get; }

    public IReadOnlyDictionary<string, RedisVLProperty> ByClrName { get; }

    public static RedisVLRecordModel Build(Type recordType, VectorStoreCollectionDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(recordType);

        return definition is { Properties.Count: > 0 }
            ? BuildFromDefinition(recordType, definition)
            : BuildFromAttributes(recordType);
    }

    /// <summary>Resolves the vector property to search against, optionally by CLR property name.</summary>
    public RedisVLProperty ResolveVector(string? clrPropertyName)
    {
        if (Vectors.Count == 0)
        {
            throw new InvalidOperationException("The record type does not define any vector properties.");
        }

        if (string.IsNullOrWhiteSpace(clrPropertyName))
        {
            if (Vectors.Count > 1)
            {
                throw new InvalidOperationException(
                    "The record type defines multiple vector properties; set VectorSearchOptions.VectorProperty to choose one.");
            }

            return Vectors[0];
        }

        var match = Vectors.FirstOrDefault(v =>
            string.Equals(v.Property.Name, clrPropertyName, StringComparison.Ordinal));
        return match ?? throw new InvalidOperationException(
            $"Vector property '{clrPropertyName}' was not found on the record type.");
    }

    public SearchSchema BuildSchema(string indexName, string keyPrefix)
    {
        var fields = new List<FieldDefinition>();
        foreach (var property in Data)
        {
            switch (property.Kind)
            {
                case RedisVLFieldKind.Tag:
                    fields.Add(new TagFieldDefinition(property.JsonName));
                    break;
                case RedisVLFieldKind.Text:
                    fields.Add(new TextFieldDefinition(property.JsonName));
                    break;
                case RedisVLFieldKind.Numeric:
                    fields.Add(new NumericFieldDefinition(property.JsonName, sortable: true));
                    break;
            }
        }

        foreach (var vector in Vectors)
        {
            fields.Add(new VectorFieldDefinition(
                vector.JsonName,
                new VectorFieldAttributes(
                    vector.Algorithm,
                    vector.DataType,
                    vector.Metric,
                    vector.Dimensions)));
        }

        return new SearchSchema(
            new IndexDefinition(indexName, keyPrefix, StorageType.Json),
            fields);
    }

    private static RedisVLRecordModel BuildFromAttributes(Type recordType)
    {
        RedisVLProperty? key = null;
        var data = new List<RedisVLProperty>();
        var vectors = new List<RedisVLProperty>();

        foreach (var property in recordType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead)
            {
                continue;
            }

            var jsonName = ResolveJsonName(property);

            if (property.GetCustomAttribute<VectorStoreKeyAttribute>() is not null)
            {
                key = new RedisVLProperty(property, jsonName, RedisVLFieldKind.Key);
                continue;
            }

            if (property.GetCustomAttribute<VectorStoreVectorAttribute>() is { } vectorAttribute)
            {
                vectors.Add(BuildVector(
                    property,
                    jsonName,
                    vectorAttribute.Dimensions,
                    vectorAttribute.DistanceFunction,
                    vectorAttribute.IndexKind));
                continue;
            }

            if (property.GetCustomAttribute<VectorStoreDataAttribute>() is { } dataAttribute)
            {
                data.Add(BuildData(property, jsonName, dataAttribute.IsIndexed, dataAttribute.IsFullTextIndexed));
            }
        }

        return Finish(recordType, key, data, vectors);
    }

    private static RedisVLRecordModel BuildFromDefinition(Type recordType, VectorStoreCollectionDefinition definition)
    {
        RedisVLProperty? key = null;
        var data = new List<RedisVLProperty>();
        var vectors = new List<RedisVLProperty>();

        foreach (var definitionProperty in definition.Properties)
        {
            var property = recordType.GetProperty(
                definitionProperty.Name,
                BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"Definition property '{definitionProperty.Name}' was not found on record type '{recordType.Name}'.");

            var jsonName = ResolveJsonName(property);

            switch (definitionProperty)
            {
                case VectorStoreKeyProperty:
                    key = new RedisVLProperty(property, jsonName, RedisVLFieldKind.Key);
                    break;
                case VectorStoreVectorProperty vectorProperty:
                    vectors.Add(BuildVector(
                        property,
                        jsonName,
                        vectorProperty.Dimensions,
                        vectorProperty.DistanceFunction,
                        vectorProperty.IndexKind));
                    break;
                case VectorStoreDataProperty dataProperty:
                    data.Add(BuildData(property, jsonName, dataProperty.IsIndexed, dataProperty.IsFullTextIndexed));
                    break;
            }
        }

        return Finish(recordType, key, data, vectors);
    }

    private static RedisVLRecordModel Finish(
        Type recordType,
        RedisVLProperty? key,
        List<RedisVLProperty> data,
        List<RedisVLProperty> vectors)
    {
        if (key is null)
        {
            throw new InvalidOperationException(
                $"Record type '{recordType.Name}' must define a key property ([VectorStoreKey] or a VectorStoreKeyProperty).");
        }

        if (key.Property.PropertyType != typeof(string))
        {
            throw new NotSupportedException(
                "The RedisVL vector-data connector currently supports string key properties only.");
        }

        return new RedisVLRecordModel(key, data, vectors);
    }

    private static RedisVLProperty BuildData(PropertyInfo property, string jsonName, bool isIndexed, bool isFullText)
    {
        if (!isIndexed && !isFullText)
        {
            return new RedisVLProperty(property, jsonName, RedisVLFieldKind.Unindexed);
        }

        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (isFullText)
        {
            if (type != typeof(string))
            {
                throw new NotSupportedException(
                    $"Full-text indexing is only supported on string properties (property '{property.Name}').");
            }

            return new RedisVLProperty(property, jsonName, RedisVLFieldKind.Text);
        }

        var kind = ResolveDataKind(type)
            ?? throw new NotSupportedException(
                $"Property '{property.Name}' of type '{property.PropertyType.Name}' cannot be indexed by the RedisVL connector.");

        return new RedisVLProperty(property, jsonName, kind);
    }

    private static RedisVLFieldKind? ResolveDataKind(Type type)
    {
        if (type == typeof(string) || type == typeof(bool) || type.IsEnum)
        {
            return RedisVLFieldKind.Tag;
        }

        if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort)
            || type == typeof(int) || type == typeof(uint) || type == typeof(long) || type == typeof(ulong)
            || type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return RedisVLFieldKind.Numeric;
        }

        if (IsStringCollection(type))
        {
            return RedisVLFieldKind.Tag;
        }

        return null;
    }

    private static bool IsStringCollection(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        if (type.IsArray)
        {
            return type.GetElementType() == typeof(string);
        }

        return type.GetInterfaces()
            .Append(type)
            .Any(i => i.IsGenericType
                && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                && i.GetGenericArguments()[0] == typeof(string));
    }

    private static RedisVLProperty BuildVector(
        PropertyInfo property,
        string jsonName,
        int dimensions,
        string? distanceFunction,
        string? indexKind)
    {
        if (dimensions <= 0)
        {
            throw new InvalidOperationException(
                $"Vector property '{property.Name}' must declare a positive dimension count.");
        }

        return new RedisVLProperty(property, jsonName, RedisVLFieldKind.Vector)
        {
            Dimensions = dimensions,
            Metric = MapDistance(distanceFunction),
            Algorithm = MapAlgorithm(indexKind),
            DataType = MapVectorDataType(property.PropertyType),
        };
    }

    private static VectorDistanceMetric MapDistance(string? distanceFunction) =>
        distanceFunction switch
        {
            DistanceFunction.CosineDistance or DistanceFunction.CosineSimilarity => VectorDistanceMetric.Cosine,
            DistanceFunction.DotProductSimilarity or DistanceFunction.NegativeDotProductSimilarity => VectorDistanceMetric.InnerProduct,
            DistanceFunction.EuclideanDistance => VectorDistanceMetric.L2,
            null or "" => VectorDistanceMetric.Cosine,
            _ => throw new NotSupportedException($"Distance function '{distanceFunction}' is not supported by the RedisVL connector."),
        };

    private static VectorAlgorithm MapAlgorithm(string? indexKind) =>
        indexKind switch
        {
            IndexKind.Flat => VectorAlgorithm.Flat,
            IndexKind.Hnsw or IndexKind.Dynamic or null or "" => VectorAlgorithm.Hnsw,
            _ => throw new NotSupportedException($"Index kind '{indexKind}' is not supported by the RedisVL connector."),
        };

    private static VectorDataType MapVectorDataType(Type propertyType)
    {
        var element = GetVectorElementType(propertyType)
            ?? throw new NotSupportedException(
                $"Vector property type '{propertyType.Name}' is not supported. Use ReadOnlyMemory<float>, float[], ReadOnlyMemory<double>, double[], or Embedding<float>.");

        if (element == typeof(float))
        {
            return VectorDataType.Float32;
        }

        if (element == typeof(double))
        {
            return VectorDataType.Float64;
        }

        throw new NotSupportedException(
            $"Vector element type '{element.Name}' is not supported. Use float or double element types.");
    }

    internal static Type? GetVectorElementType(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(ReadOnlyMemory<>) || definition == typeof(Memory<>) || definition == typeof(Embedding<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static string ResolveJsonName(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);
}
