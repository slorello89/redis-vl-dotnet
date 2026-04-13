using System.Collections.ObjectModel;
using System.Globalization;
using RedisVlDotNet.Schema;
using StackExchange.Redis;

namespace RedisVlDotNet.Indexes;

internal static class SearchIndexSchemaBuilder
{
    public static SearchSchema FromInfo(SearchIndexInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        var indexDefinition = ParseEntryMap(GetRequiredValue(info, "index_definition"), "index_definition");
        var storageType = ParseStorageType(GetRequiredString(indexDefinition, "key_type", "index_definition"));
        var prefixes = ParsePrefixes(indexDefinition);
        var keySeparator = ParseOptionalChar(indexDefinition, "separator", defaultValue: ':');
        var stopwords = ParseStopwords(info);
        var indexOptions = ParseOptionSet(info);
        var temporarySeconds = ParseTemporarySeconds(indexOptions);
        var fields = ParseFields(info, storageType);

        return new SearchSchema(
            new IndexDefinition(
                info.Name,
                prefixes,
                storageType,
                keySeparator: keySeparator,
                stopwords: stopwords,
                maxTextFields: indexOptions.Contains("MAXTEXTFIELDS"),
                temporarySeconds: temporarySeconds,
                noOffsets: indexOptions.Contains("NOOFFSETS"),
                noHighlight: indexOptions.Contains("NOHL"),
                noFields: indexOptions.Contains("NOFIELDS"),
                noFrequencies: indexOptions.Contains("NOFREQS"),
                skipInitialScan: indexOptions.Contains("SKIPINITIALSCAN")),
            fields);
    }

    private static IReadOnlyList<FieldDefinition> ParseFields(SearchIndexInfo info, StorageType storageType)
    {
        var attributes = GetRequiredValue(info, "attributes");
        var rows = attributes.Resp2Type == ResultType.Array
            ? (RedisResult[]?)attributes ?? []
            : throw new InvalidOperationException("Redis FT.INFO response must contain attribute rows.");

        return new ReadOnlyCollection<FieldDefinition>(rows.Select(row => ParseField(row, storageType)).ToList());
    }

    private static FieldDefinition ParseField(RedisResult row, StorageType storageType)
    {
        var attributes = ParseEntryMap(row, "attributes");
        var identifier = GetRequiredString(attributes, "identifier", "attributes");
        var attributeName = GetOptionalString(attributes, "attribute");
        var name = ResolveFieldName(storageType, identifier, attributeName);
        var alias = ResolveFieldAlias(storageType, identifier, attributeName);
        var type = NormalizeToken(GetRequiredString(attributes, "type", "attributes"));

        return type switch
        {
            "TEXT" => new TextFieldDefinition(
                name,
                alias: alias,
                sortable: IsSortable(attributes),
                noStem: HasFlag(attributes, "NOSTEM"),
                phoneticMatch: HasNonEmptyValue(attributes, "PHONETIC"),
                weight: ParseOptionalDouble(attributes, "WEIGHT", 1d),
                withSuffixTrie: HasFlag(attributes, "WITHSUFFIXTRIE"),
                indexMissing: HasFlag(attributes, "INDEXMISSING"),
                indexEmpty: HasFlag(attributes, "INDEXEMPTY"),
                noIndex: HasFlag(attributes, "NOINDEX"),
                unNormalizedForm: HasFlag(attributes, "UNF") || HasFlagValue(attributes, "SORTABLE", "UNF")),
            "TAG" => new TagFieldDefinition(
                name,
                alias: alias,
                sortable: IsSortable(attributes),
                separator: ParseOptionalChar(attributes, "SEPARATOR", defaultValue: ','),
                caseSensitive: HasFlag(attributes, "CASESENSITIVE"),
                withSuffixTrie: HasFlag(attributes, "WITHSUFFIXTRIE"),
                indexMissing: HasFlag(attributes, "INDEXMISSING"),
                indexEmpty: HasFlag(attributes, "INDEXEMPTY"),
                noIndex: HasFlag(attributes, "NOINDEX")),
            "NUMERIC" => new NumericFieldDefinition(
                name,
                alias: alias,
                sortable: IsSortable(attributes),
                indexMissing: HasFlag(attributes, "INDEXMISSING"),
                noIndex: HasFlag(attributes, "NOINDEX"),
                unNormalizedForm: HasFlag(attributes, "UNF") || HasFlagValue(attributes, "SORTABLE", "UNF")),
            "GEO" => new GeoFieldDefinition(
                name,
                alias: alias,
                sortable: IsSortable(attributes),
                indexMissing: HasFlag(attributes, "INDEXMISSING"),
                noIndex: HasFlag(attributes, "NOINDEX")),
            "VECTOR" => new VectorFieldDefinition(
                name,
                new VectorFieldAttributes(
                    ParseVectorAlgorithm(GetRequiredString(attributes, "algorithm", "attributes")),
                    ParseVectorDataType(GetRequiredString(attributes, "data_type", "attributes")),
                    ParseVectorDistanceMetric(GetRequiredString(attributes, "distance_metric", "attributes")),
                    ParseRequiredInt(attributes, "dim", "attributes"),
                    initialCapacity: ParseOptionalInt(attributes, "initial_cap"),
                    blockSize: ParseOptionalInt(attributes, "block_size"),
                    m: ParseOptionalInt(attributes, "m"),
                    efConstruction: ParseOptionalInt(attributes, "ef_construction"),
                    efRuntime: ParseOptionalInt(attributes, "ef_runtime")),
                alias: alias,
                indexMissing: HasFlag(attributes, "INDEXMISSING")),
            _ => throw new InvalidOperationException($"Redis FT.INFO contained unsupported field type '{type}'.")
        };
    }

    private static IReadOnlyList<string> ParsePrefixes(IReadOnlyDictionary<string, RedisResult> indexDefinition)
    {
        if (!TryGetValue(indexDefinition, "prefixes", out var prefixesValue) || prefixesValue.IsNull)
        {
            throw new InvalidOperationException("Redis FT.INFO response did not include index prefixes.");
        }

        var prefixes = ((RedisResult[]?)prefixesValue ?? [])
            .Select(static entry => entry.ToString())
            .Where(static entry => !string.IsNullOrWhiteSpace(entry))
            .Select(static entry => entry!.Trim())
            .ToArray();

        if (prefixes.Length == 0)
        {
            throw new InvalidOperationException("Redis FT.INFO response did not include index prefixes.");
        }

        return prefixes;
    }

    private static IReadOnlyList<string>? ParseStopwords(SearchIndexInfo info)
    {
        if (!info.TryGetValue("stopwords_list", out var stopwordsValue) || stopwordsValue.IsNull)
        {
            return null;
        }

        return ((RedisResult[]?)stopwordsValue ?? [])
            .Select(static entry => entry.ToString())
            .Where(static entry => !string.IsNullOrWhiteSpace(entry))
            .Select(static entry => entry!.Trim())
            .ToArray();
    }

    private static HashSet<string> ParseOptionSet(SearchIndexInfo info)
    {
        if (!info.TryGetValue("index_options", out var optionsValue) || optionsValue.IsNull)
        {
            return [];
        }

        return FlattenResult(optionsValue)
            .Select(NormalizeToken)
            .Where(static entry => !string.IsNullOrEmpty(entry))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static int ParseTemporarySeconds(IReadOnlySet<string> options)
    {
        if (!options.Contains("TEMPORARY"))
        {
            return 0;
        }

        var value = options.FirstOrDefault(static option =>
            int.TryParse(option, NumberStyles.Integer, CultureInfo.InvariantCulture, out _));

        if (value is null)
        {
            throw new InvalidOperationException("Redis FT.INFO index options included TEMPORARY without an expiration value.");
        }

        return int.Parse(value, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyDictionary<string, RedisResult> ParseEntryMap(RedisResult result, string context)
    {
        var entries = result.Resp2Type == ResultType.Array
            ? (RedisResult[]?)result ?? []
            : throw new InvalidOperationException($"Redis FT.INFO {context} response must contain key-value pairs.");

        if (entries.Length % 2 != 0)
        {
            throw new InvalidOperationException($"Redis FT.INFO {context} response must contain key-value pairs.");
        }

        var dictionary = new Dictionary<string, RedisResult>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < entries.Length; index += 2)
        {
            var key = entries[index].ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            dictionary[key.Trim()] = entries[index + 1];
        }

        return dictionary;
    }

    private static IEnumerable<string> FlattenResult(RedisResult result)
    {
        if (result.IsNull)
        {
            yield break;
        }

        if (result.Resp2Type == ResultType.Array)
        {
            foreach (var entry in (RedisResult[]?)result ?? [])
            {
                foreach (var item in FlattenResult(entry))
                {
                    yield return item;
                }
            }

            yield break;
        }

        var value = result.ToString();
        if (!string.IsNullOrWhiteSpace(value))
        {
            yield return value.Trim();
        }
    }

    private static RedisResult GetRequiredValue(SearchIndexInfo info, string key)
    {
        if (info.TryGetValue(key, out var value) && !value.IsNull)
        {
            return value;
        }

        throw new InvalidOperationException($"Redis FT.INFO response did not include {key}.");
    }

    private static string GetRequiredString(IReadOnlyDictionary<string, RedisResult> attributes, string key, string context)
    {
        var value = GetOptionalString(attributes, key);
        return value ?? throw new InvalidOperationException($"Redis FT.INFO {context} response did not include {key}.");
    }

    private static string? GetOptionalString(IReadOnlyDictionary<string, RedisResult> attributes, string key) =>
        TryGetValue(attributes, key, out var value) && !value.IsNull
            ? value.ToString()?.Trim()
            : null;

    private static bool TryGetValue(IReadOnlyDictionary<string, RedisResult> attributes, string key, out RedisResult value)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (attributes.TryGetValue(key, out var directValue)
            || attributes.TryGetValue(key.ToUpperInvariant(), out directValue)
            || attributes.TryGetValue(key.ToLowerInvariant(), out directValue))
        {
            value = directValue;
            return true;
        }

        value = default!;
        return false;
    }

    private static bool HasFlag(IReadOnlyDictionary<string, RedisResult> attributes, string key)
    {
        if (!TryGetValue(attributes, key, out var value))
        {
            return false;
        }

        if (value.IsNull)
        {
            return true;
        }

        var raw = value.ToString();
        return string.IsNullOrWhiteSpace(raw)
            || !raw.Equals("false", StringComparison.OrdinalIgnoreCase)
               && !raw.Equals("0", StringComparison.OrdinalIgnoreCase)
               && !raw.Equals("no", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasFlagValue(IReadOnlyDictionary<string, RedisResult> attributes, string key, string expectedValue) =>
        TryGetValue(attributes, key, out var value)
        && string.Equals(value.ToString(), expectedValue, StringComparison.OrdinalIgnoreCase);

    private static bool HasNonEmptyValue(IReadOnlyDictionary<string, RedisResult> attributes, string key) =>
        TryGetValue(attributes, key, out var value) && !string.IsNullOrWhiteSpace(value.ToString());

    private static bool IsSortable(IReadOnlyDictionary<string, RedisResult> attributes) =>
        HasFlag(attributes, "SORTABLE") || HasFlagValue(attributes, "SORTABLE", "UNF");

    private static double ParseOptionalDouble(IReadOnlyDictionary<string, RedisResult> attributes, string key, double defaultValue)
    {
        var raw = GetOptionalString(attributes, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        return double.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Redis FT.INFO attribute {key} value '{raw}' was not numeric.");
    }

    private static int ParseRequiredInt(IReadOnlyDictionary<string, RedisResult> attributes, string key, string context)
    {
        var raw = GetRequiredString(attributes, key, context);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Redis FT.INFO {context} value '{key}' was not an integer.");
    }

    private static int ParseOptionalInt(IReadOnlyDictionary<string, RedisResult> attributes, string key)
    {
        var raw = GetOptionalString(attributes, key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Redis FT.INFO attribute {key} value '{raw}' was not an integer.");
    }

    private static char ParseOptionalChar(IReadOnlyDictionary<string, RedisResult> attributes, string key, char defaultValue)
    {
        var raw = GetOptionalString(attributes, key);
        if (string.IsNullOrEmpty(raw))
        {
            return defaultValue;
        }

        return raw.Length == 1
            ? raw[0]
            : throw new InvalidOperationException($"Redis FT.INFO attribute {key} value '{raw}' was not a single character.");
    }

    private static StorageType ParseStorageType(string value) =>
        NormalizeToken(value) switch
        {
            "HASH" => StorageType.Hash,
            "JSON" => StorageType.Json,
            _ => throw new InvalidOperationException($"Redis FT.INFO contained unsupported storage type '{value}'.")
        };

    private static VectorAlgorithm ParseVectorAlgorithm(string value) =>
        NormalizeToken(value) switch
        {
            "FLAT" => VectorAlgorithm.Flat,
            "HNSW" => VectorAlgorithm.Hnsw,
            _ => throw new InvalidOperationException($"Redis FT.INFO contained unsupported vector algorithm '{value}'.")
        };

    private static VectorDataType ParseVectorDataType(string value) =>
        NormalizeToken(value) switch
        {
            "FLOAT32" => VectorDataType.Float32,
            "FLOAT64" => VectorDataType.Float64,
            "FLOAT16" => VectorDataType.Float16,
            "BFLOAT16" => VectorDataType.BFloat16,
            "UINT8" => VectorDataType.UInt8,
            "INT8" => VectorDataType.Int8,
            _ => throw new InvalidOperationException($"Redis FT.INFO contained unsupported vector data type '{value}'.")
        };

    private static VectorDistanceMetric ParseVectorDistanceMetric(string value) =>
        NormalizeToken(value) switch
        {
            "COSINE" => VectorDistanceMetric.Cosine,
            "L2" => VectorDistanceMetric.L2,
            "IP" => VectorDistanceMetric.InnerProduct,
            _ => throw new InvalidOperationException($"Redis FT.INFO contained unsupported vector distance metric '{value}'.")
        };

    private static string ResolveFieldName(StorageType storageType, string identifier, string? attributeName)
    {
        if (storageType != StorageType.Json)
        {
            return identifier;
        }

        if (!string.IsNullOrWhiteSpace(attributeName)
            && string.Equals(attributeName, GetDefaultJsonAlias(identifier), StringComparison.Ordinal))
        {
            return attributeName;
        }

        return identifier;
    }

    private static string? ResolveFieldAlias(StorageType storageType, string identifier, string? attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName))
        {
            return null;
        }

        if (storageType == StorageType.Json
            && string.Equals(attributeName, GetDefaultJsonAlias(identifier), StringComparison.Ordinal))
        {
            return null;
        }

        return string.Equals(attributeName, identifier, StringComparison.Ordinal) ? null : attributeName;
    }

    private static string GetDefaultJsonAlias(string identifier) =>
        identifier.TrimStart('$').TrimStart('.');

    private static string NormalizeToken(string value) =>
        value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
}
