using System.Collections.ObjectModel;
using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

    public static SearchSchema FromYaml(string yaml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(yaml);

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        YamlSchemaDocument? document;

        try
        {
            document = deserializer.Deserialize<YamlSchemaDocument>(yaml);
        }
        catch (YamlException exception)
        {
            throw new ArgumentException("YAML schema content could not be parsed.", nameof(yaml), exception);
        }

        if (document is null)
        {
            throw new ArgumentException("YAML schema content could not be parsed.", nameof(yaml));
        }

        return document.ToSearchSchema();
    }

    public static SearchSchema FromYamlFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return FromYaml(File.ReadAllText(path));
    }

    private sealed class YamlSchemaDocument
    {
        public YamlIndexDefinition? Index { get; init; }

        public List<YamlFieldDefinition>? Fields { get; init; }

        public SearchSchema ToSearchSchema()
        {
            if (Index is null)
            {
                throw new ArgumentException("Schema YAML must include an index definition.", nameof(Index));
            }

            if (Fields is null)
            {
                throw new ArgumentException("Schema YAML must include a fields collection.", nameof(Fields));
            }

            return new SearchSchema(
                new IndexDefinition(
                    Index.Name!,
                    ParsePrefixes(Index),
                    ParseEnum<StorageType>(Index.StorageType, "index.storage_type"),
                    ParseKeySeparator(Index.KeySeparator),
                    ParseStopwords(Index.Stopwords),
                    Index.MaxTextFields,
                    ParseOptionalInt(Index.Temporary, "index.temporary"),
                    Index.NoOffsets,
                    Index.NoHighlight,
                    Index.NoFields,
                    Index.NoFrequencies,
                    Index.SkipInitialScan),
                Fields.Select(MapField));
        }

        private static FieldDefinition MapField(YamlFieldDefinition field)
        {
            ArgumentNullException.ThrowIfNull(field);

            var fieldType = NormalizeRequired(field.Type, "fields.type");
            var name = NormalizeRequired(field.Name, "fields.name");
            var alias = NormalizeOptional(field.Alias);

            return fieldType.ToLowerInvariant() switch
            {
                "text" => new TextFieldDefinition(
                    name,
                    alias: alias,
                    sortable: field.Sortable,
                    noStem: field.NoStem,
                    phoneticMatch: field.PhoneticMatch,
                    weight: ParseOptionalDouble(field.Weight, "fields.weight", 1d),
                    withSuffixTrie: field.WithSuffixTrie,
                    indexMissing: field.IndexMissing,
                    indexEmpty: field.IndexEmpty,
                    noIndex: field.NoIndex,
                    unNormalizedForm: field.Unf),
                "tag" => new TagFieldDefinition(
                    name,
                    alias: alias,
                    sortable: field.Sortable,
                    separator: ParseSeparator(field.Separator),
                    caseSensitive: field.CaseSensitive,
                    withSuffixTrie: field.WithSuffixTrie,
                    indexMissing: field.IndexMissing,
                    indexEmpty: field.IndexEmpty,
                    noIndex: field.NoIndex),
                "numeric" => new NumericFieldDefinition(
                    name,
                    alias: alias,
                    sortable: field.Sortable,
                    indexMissing: field.IndexMissing,
                    noIndex: field.NoIndex,
                    unNormalizedForm: field.Unf),
                "geo" => new GeoFieldDefinition(
                    name,
                    alias: alias,
                    sortable: field.Sortable,
                    indexMissing: field.IndexMissing,
                    noIndex: field.NoIndex),
                "vector" => new VectorFieldDefinition(
                    name,
                    MapVectorAttributes(field.Attributes),
                    alias: alias,
                    indexMissing: field.IndexMissing),
                _ => throw new ArgumentException($"Unsupported schema field type '{field.Type}'.", nameof(field.Type))
            };
        }

        private static IReadOnlyList<string> ParsePrefixes(YamlIndexDefinition index)
        {
            ArgumentNullException.ThrowIfNull(index);

            var hasPrefix = !string.IsNullOrWhiteSpace(index.Prefix);
            var hasPrefixes = index.Prefixes is { Count: > 0 };

            if (hasPrefix && hasPrefixes)
            {
                throw new ArgumentException("Schema YAML must define either index.prefix or index.prefixes, not both.", nameof(index));
            }

            if (hasPrefixes)
            {
                return index.Prefixes!
                    .Select(static prefix => string.IsNullOrWhiteSpace(prefix)
                        ? throw new ArgumentException("Index prefixes cannot contain blank values.", nameof(index))
                        : prefix.Trim())
                    .ToArray();
            }

            return [NormalizeRequired(index.Prefix, "index.prefix")];
        }

        private static VectorFieldAttributes MapVectorAttributes(YamlVectorAttributes? attributes)
        {
            if (attributes is null)
            {
                throw new ArgumentException("Vector fields must define attrs.", nameof(attributes));
            }

            return new VectorFieldAttributes(
                ParseEnum<VectorAlgorithm>(attributes.Algorithm, "fields.attrs.algorithm"),
                ParseEnum<VectorDataType>(attributes.DataType, "fields.attrs.datatype"),
                ParseEnum<VectorDistanceMetric>(attributes.DistanceMetric, "fields.attrs.distance_metric"),
                ParseRequiredInt(attributes.Dimensions, "fields.attrs.dims"),
                initialCapacity: ParseOptionalInt(attributes.InitialCapacity, "fields.attrs.initial_capacity"),
                blockSize: ParseOptionalInt(attributes.BlockSize, "fields.attrs.block_size"),
                m: ParseOptionalInt(attributes.M, "fields.attrs.m"),
                efConstruction: ParseOptionalInt(attributes.EfConstruction, "fields.attrs.ef_construction"),
                efRuntime: ParseOptionalInt(attributes.EfRuntime, "fields.attrs.ef_runtime"));
        }

        private static TEnum ParseEnum<TEnum>(string? rawValue, string paramName)
            where TEnum : struct, Enum
        {
            var normalized = NormalizeRequired(rawValue, paramName).Replace("-", string.Empty, StringComparison.Ordinal);
            if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Unsupported value '{rawValue}' for {paramName}.", paramName);
        }

        private static int ParseRequiredInt(string? rawValue, string paramName)
        {
            if (int.TryParse(NormalizeRequired(rawValue, paramName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Value '{rawValue}' for {paramName} must be an integer.", paramName);
        }

        private static int ParseOptionalInt(string? rawValue, string paramName)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return 0;
            }

            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Value '{rawValue}' for {paramName} must be an integer.", paramName);
        }

        private static double ParseOptionalDouble(string? rawValue, string paramName, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return defaultValue;
            }

            if (double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Value '{rawValue}' for {paramName} must be numeric.", paramName);
        }

        private static char ParseSeparator(string? rawValue)
        {
            var normalized = NormalizeOptional(rawValue);
            return string.IsNullOrEmpty(normalized) ? ',' : normalized.Length == 1
                ? normalized[0]
                : throw new ArgumentException("Tag field separator must be a single character.", nameof(rawValue));
        }

        private static char ParseKeySeparator(string? rawValue)
        {
            var normalized = NormalizeOptional(rawValue);
            if (string.IsNullOrEmpty(normalized))
            {
                return ':';
            }

            return normalized.Length == 1 && !char.IsWhiteSpace(normalized[0])
                ? normalized[0]
                : throw new ArgumentException("Index key separator must be a single non-whitespace character.", nameof(rawValue));
        }

        private static IReadOnlyList<string>? ParseStopwords(List<string>? values)
        {
            if (values is null)
            {
                return null;
            }

            return values
                .Select(static stopword => string.IsNullOrWhiteSpace(stopword)
                    ? throw new ArgumentException("Index stopwords cannot contain blank values.", nameof(values))
                    : stopword.Trim())
                .ToArray();
        }

        private static string NormalizeRequired(string? value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Schema YAML is missing required value '{paramName}'.", paramName);
            }

            return value.Trim();
        }

        private static string? NormalizeOptional(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class YamlIndexDefinition
    {
        public string? Name { get; init; }

        public string? Prefix { get; init; }

        public List<string>? Prefixes { get; init; }

        public string? KeySeparator { get; init; }

        public string? StorageType { get; init; }

        public List<string>? Stopwords { get; init; }

        public bool MaxTextFields { get; init; }

        public string? Temporary { get; init; }

        public bool NoOffsets { get; init; }

        public bool NoHighlight { get; init; }

        public bool NoFields { get; init; }

        public bool NoFrequencies { get; init; }

        public bool SkipInitialScan { get; init; }
    }

    private sealed class YamlFieldDefinition
    {
        public string? Name { get; init; }

        public string? Type { get; init; }

        public string? Alias { get; init; }

        public bool Sortable { get; init; }

        public bool NoStem { get; init; }

        public bool PhoneticMatch { get; init; }

        public string? Weight { get; init; }

        public string? Separator { get; init; }

        public bool CaseSensitive { get; init; }

        public bool WithSuffixTrie { get; init; }

        public bool IndexMissing { get; init; }

        public bool IndexEmpty { get; init; }

        public bool NoIndex { get; init; }

        [YamlMember(Alias = "unf")]
        public bool Unf { get; init; }

        [YamlMember(Alias = "attrs")]
        public YamlVectorAttributes? Attributes { get; init; }
    }

    private sealed class YamlVectorAttributes
    {
        public string? Algorithm { get; init; }

        [YamlMember(Alias = "datatype")]
        public string? DataType { get; init; }

        public string? DistanceMetric { get; init; }

        [YamlMember(Alias = "dims")]
        public string? Dimensions { get; init; }

        public string? InitialCapacity { get; init; }

        public string? BlockSize { get; init; }

        public string? M { get; init; }

        public string? EfConstruction { get; init; }

        public string? EfRuntime { get; init; }
    }
}
