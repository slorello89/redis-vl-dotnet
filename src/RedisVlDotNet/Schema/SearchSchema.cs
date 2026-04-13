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
            .IgnoreUnmatchedProperties()
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
                    Index.Prefix!,
                    ParseEnum<StorageType>(Index.StorageType, "index.storage_type"),
                    ParseKeySeparator(Index.KeySeparator),
                    ParseStopwords(Index.Stopwords)),
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
                    phoneticMatch: field.PhoneticMatch),
                "tag" => new TagFieldDefinition(
                    name,
                    alias: alias,
                    sortable: field.Sortable,
                    separator: ParseSeparator(field.Separator),
                    caseSensitive: field.CaseSensitive),
                "numeric" => new NumericFieldDefinition(name, alias: alias, sortable: field.Sortable),
                "geo" => new GeoFieldDefinition(name, alias: alias, sortable: field.Sortable),
                "vector" => new VectorFieldDefinition(
                    name,
                    MapVectorAttributes(field.Attributes),
                    alias: alias),
                _ => throw new ArgumentException($"Unsupported schema field type '{field.Type}'.", nameof(field.Type))
            };
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
                initialCapacity: ParseOptionalInt(attributes.InitialCapacity),
                blockSize: ParseOptionalInt(attributes.BlockSize),
                m: ParseOptionalInt(attributes.M),
                efConstruction: ParseOptionalInt(attributes.EfConstruction),
                efRuntime: ParseOptionalInt(attributes.EfRuntime));
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

        private static int ParseOptionalInt(string? rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return 0;
            }

            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            throw new ArgumentException($"Value '{rawValue}' must be an integer.", nameof(rawValue));
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

        public string? KeySeparator { get; init; }

        public string? StorageType { get; init; }

        public List<string>? Stopwords { get; init; }
    }

    private sealed class YamlFieldDefinition
    {
        public string? Name { get; init; }

        public string? Type { get; init; }

        public string? Alias { get; init; }

        public bool Sortable { get; init; }

        public bool NoStem { get; init; }

        public bool PhoneticMatch { get; init; }

        public string? Separator { get; init; }

        public bool CaseSensitive { get; init; }

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
