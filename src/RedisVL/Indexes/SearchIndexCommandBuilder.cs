using System.Globalization;
using RedisVL.Schema;

namespace RedisVL.Indexes;

internal static class SearchIndexCommandBuilder
{
    public static object[] BuildCreateArguments(SearchSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var arguments = new List<object>
        {
            schema.Index.Name,
            "ON",
            ToRedisKeyword(schema.Index.StorageType),
            "PREFIX",
            schema.Index.Prefixes.Count.ToString(CultureInfo.InvariantCulture)
        };
        arguments.AddRange(schema.Index.Prefixes);
        AddIndexOption(arguments, "MAXTEXTFIELDS", schema.Index.MaxTextFields);
        if (schema.Index.TemporarySeconds > 0)
        {
            arguments.Add("TEMPORARY");
            arguments.Add(schema.Index.TemporarySeconds.ToString(CultureInfo.InvariantCulture));
        }

        AddIndexOption(arguments, "NOOFFSETS", schema.Index.NoOffsets);
        AddIndexOption(arguments, "NOHL", schema.Index.NoHighlight);
        AddIndexOption(arguments, "NOFIELDS", schema.Index.NoFields);
        AddIndexOption(arguments, "NOFREQS", schema.Index.NoFrequencies);
        AddIndexOption(arguments, "SKIPINITIALSCAN", schema.Index.SkipInitialScan);
        if (schema.Index.Stopwords is not null)
        {
            arguments.Add("STOPWORDS");
            arguments.Add(schema.Index.Stopwords.Count.ToString(CultureInfo.InvariantCulture));
            arguments.AddRange(schema.Index.Stopwords);
        }

        arguments.Add("SCHEMA");

        foreach (var field in schema.Fields)
        {
            AddFieldArguments(arguments, schema.Index.StorageType, field);
        }

        return arguments.ToArray();
    }

    public static object[] BuildDropArguments(SearchSchema schema, bool deleteDocuments)
    {
        ArgumentNullException.ThrowIfNull(schema);

        return deleteDocuments
            ? [schema.Index.Name, "DD"]
            : [schema.Index.Name];
    }

    private static void AddFieldArguments(List<object> arguments, StorageType storageType, FieldDefinition field)
    {
        var identifier = storageType == StorageType.Json
            ? ToJsonPath(field.Name)
            : field.Name;
        var alias = storageType == StorageType.Json
            ? field.Alias ?? GetDefaultJsonAlias(field.Name)
            : field.Alias;

        arguments.Add(identifier);
        if (!string.IsNullOrWhiteSpace(alias))
        {
            arguments.Add("AS");
            arguments.Add(alias!);
        }

        switch (field)
        {
            case TextFieldDefinition textField:
                arguments.Add("TEXT");
                if (textField.Weight != 1d)
                {
                    arguments.Add("WEIGHT");
                    arguments.Add(textField.Weight.ToString("0.################", CultureInfo.InvariantCulture));
                }

                if (textField.NoStem)
                {
                    arguments.Add("NOSTEM");
                }

                if (textField.PhoneticMatch)
                {
                    arguments.Add("PHONETIC");
                    arguments.Add("dm:en");
                }

                AddFieldOption(arguments, "WITHSUFFIXTRIE", textField.WithSuffixTrie);
                AddFieldOption(arguments, "INDEXEMPTY", textField.IndexEmpty);
                AddFieldOption(arguments, "INDEXMISSING", textField.IndexMissing);
                if (textField.Sortable)
                {
                    arguments.Add("SORTABLE");
                    if (textField.UnNormalizedForm)
                    {
                        arguments.Add("UNF");
                    }
                }

                AddFieldOption(arguments, "NOINDEX", textField.NoIndex);
                break;
            case TagFieldDefinition tagField:
                arguments.Add("TAG");
                arguments.Add("SEPARATOR");
                arguments.Add(tagField.Separator.ToString());
                if (tagField.CaseSensitive)
                {
                    arguments.Add("CASESENSITIVE");
                }

                AddFieldOption(arguments, "WITHSUFFIXTRIE", tagField.WithSuffixTrie);
                AddFieldOption(arguments, "INDEXEMPTY", tagField.IndexEmpty);
                AddFieldOption(arguments, "INDEXMISSING", tagField.IndexMissing);
                if (tagField.Sortable)
                {
                    arguments.Add("SORTABLE");
                }

                AddFieldOption(arguments, "NOINDEX", tagField.NoIndex);
                break;
            case NumericFieldDefinition numericField:
                arguments.Add("NUMERIC");
                AddFieldOption(arguments, "INDEXMISSING", numericField.IndexMissing);
                if (numericField.Sortable)
                {
                    arguments.Add("SORTABLE");
                    if (numericField.UnNormalizedForm)
                    {
                        arguments.Add("UNF");
                    }
                }

                AddFieldOption(arguments, "NOINDEX", numericField.NoIndex);
                break;
            case GeoFieldDefinition geoField:
                arguments.Add("GEO");
                AddFieldOption(arguments, "INDEXMISSING", geoField.IndexMissing);
                if (geoField.Sortable)
                {
                    arguments.Add("SORTABLE");
                }

                AddFieldOption(arguments, "NOINDEX", geoField.NoIndex);
                break;
            case VectorFieldDefinition vectorField:
                AddVectorArguments(arguments, vectorField);
                break;
            default:
                throw new InvalidOperationException($"Unsupported field definition type '{field.GetType().Name}'.");
        }
    }

    private static void AddVectorArguments(List<object> arguments, VectorFieldDefinition field)
    {
        var attributeArguments = new List<object>
        {
            "TYPE", ToRedisKeyword(field.Attributes.DataType),
            "DIM", field.Attributes.Dimensions.ToString(CultureInfo.InvariantCulture),
            "DISTANCE_METRIC", ToRedisKeyword(field.Attributes.DistanceMetric)
        };

        AddOptionalAttribute(attributeArguments, "INITIAL_CAP", field.Attributes.InitialCapacity);
        AddOptionalAttribute(attributeArguments, "BLOCK_SIZE", field.Attributes.BlockSize);
        AddOptionalAttribute(attributeArguments, "M", field.Attributes.M);
        AddOptionalAttribute(attributeArguments, "EF_CONSTRUCTION", field.Attributes.EfConstruction);
        AddOptionalAttribute(attributeArguments, "EF_RUNTIME", field.Attributes.EfRuntime);
        arguments.Add("VECTOR");
        arguments.Add(ToRedisKeyword(field.Attributes.Algorithm));
        arguments.Add(attributeArguments.Count.ToString(CultureInfo.InvariantCulture));
        arguments.AddRange(attributeArguments);
    }

    private static void AddOptionalAttribute(List<object> arguments, string keyword, int value)
    {
        if (value <= 0)
        {
            return;
        }

        arguments.Add(keyword);
        arguments.Add(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AddIndexOption(List<object> arguments, string keyword, bool enabled)
    {
        if (enabled)
        {
            arguments.Add(keyword);
        }
    }

    private static void AddFieldOption(List<object> arguments, string keyword, bool enabled)
    {
        if (enabled)
        {
            arguments.Add(keyword);
        }
    }

    private static string ToJsonPath(string value) =>
        value.StartsWith("$", StringComparison.Ordinal) ? value : $"$.{value}";

    private static string GetDefaultJsonAlias(string value)
    {
        if (!value.StartsWith("$", StringComparison.Ordinal))
        {
            return value;
        }

        return value.TrimStart('$').TrimStart('.');
    }

    private static string ToRedisKeyword(StorageType storageType) =>
        storageType switch
        {
            StorageType.Hash => "HASH",
            StorageType.Json => "JSON",
            _ => throw new ArgumentOutOfRangeException(nameof(storageType), storageType, "Unsupported storage type.")
        };

    private static string ToRedisKeyword(VectorAlgorithm algorithm) =>
        algorithm switch
        {
            VectorAlgorithm.Flat => "FLAT",
            VectorAlgorithm.Hnsw => "HNSW",
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported vector algorithm.")
        };

    private static string ToRedisKeyword(VectorDataType dataType) =>
        dataType switch
        {
            VectorDataType.Float32 => "FLOAT32",
            VectorDataType.Float64 => "FLOAT64",
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, "Unsupported vector data type.")
        };

    private static string ToRedisKeyword(VectorDistanceMetric distanceMetric) =>
        distanceMetric switch
        {
            VectorDistanceMetric.Cosine => "COSINE",
            VectorDistanceMetric.InnerProduct => "IP",
            VectorDistanceMetric.L2 => "L2",
            _ => throw new ArgumentOutOfRangeException(nameof(distanceMetric), distanceMetric, "Unsupported vector distance metric.")
        };
}
