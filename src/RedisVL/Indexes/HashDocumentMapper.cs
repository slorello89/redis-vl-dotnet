using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace RedisVL.Indexes;

internal static class HashDocumentMapper
{
    // Per-type property name/type map, computed once and reused so materializing many hash documents
    // doesn't re-run reflection for every entry. Keyed on the serializer options because the JSON name
    // resolution depends on the naming policy.
    private static readonly ConcurrentDictionary<PropertyTypeCacheKey, IReadOnlyDictionary<string, Type>> PropertyTypeCache = new();

    public static HashEntry[] ToHashEntries<TDocument>(TDocument document, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        var element = JsonSerializer.SerializeToElement(document, serializerOptions);
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Hash documents must serialize to a JSON object.", nameof(document));
        }

        var entries = new List<HashEntry>();
        foreach (var property in element.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            entries.Add(new HashEntry(property.Name, ToRedisValue(property.Value)));
        }

        return [.. entries];
    }

    public static TDocument? FromHashEntries<TDocument>(HashEntry[] entries, JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        if (entries.Length == 0)
        {
            return default;
        }

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        var propertyTypes = GetPropertyTypes(typeof(TDocument), serializerOptions);
        foreach (var entry in entries)
        {
            if (entry.Name.IsNullOrEmpty)
            {
                continue;
            }

            writer.WritePropertyName(entry.Name.ToString());
            if (entry.Value.IsNull)
            {
                writer.WriteNullValue();
                continue;
            }

            WriteRedisValue(writer, entry.Name.ToString(), entry.Value.ToString(), propertyTypes, serializerOptions);
        }

        writer.WriteEndObject();
        writer.Flush();

        return JsonSerializer.Deserialize<TDocument>(stream.ToArray(), serializerOptions);
    }

    public static HashEntry ToHashEntry(string field, object? value, JsonSerializerOptions serializerOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        if (value is null)
        {
            throw new ArgumentException("Hash partial update values cannot be null.", nameof(value));
        }

        var element = JsonSerializer.SerializeToElement(value, serializerOptions);
        return new HashEntry(field.Trim(), ToRedisValue(element));
    }

    private static RedisValue ToRedisValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array or JsonValueKind.Object => element.GetRawText(),
            _ => RedisValue.Null
        };

    private static void WriteRedisValue(
        Utf8JsonWriter writer,
        string propertyName,
        string value,
        IReadOnlyDictionary<string, Type> propertyTypes,
        JsonSerializerOptions serializerOptions)
    {
        if (propertyTypes.TryGetValue(propertyName, out var propertyType) &&
            TryWriteTypedValue(writer, value, propertyType, serializerOptions))
        {
            return;
        }

        writer.WriteStringValue(value);
    }

    private static bool TryWriteTypedValue(
        Utf8JsonWriter writer,
        string value,
        Type propertyType,
        JsonSerializerOptions serializerOptions)
    {
        var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (targetType == typeof(string))
        {
            writer.WriteStringValue(value);
            return true;
        }

        if (targetType == typeof(bool) && bool.TryParse(value, out var booleanValue))
        {
            writer.WriteBooleanValue(booleanValue);
            return true;
        }

        if (targetType == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            writer.WriteNumberValue(intValue);
            return true;
        }

        if (targetType == typeof(long) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            writer.WriteNumberValue(longValue);
            return true;
        }

        if (targetType == typeof(float) && float.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
        {
            writer.WriteNumberValue(floatValue);
            return true;
        }

        if (targetType == typeof(double) && double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
        {
            writer.WriteNumberValue(doubleValue);
            return true;
        }

        if (targetType == typeof(decimal) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            writer.WriteNumberValue(decimalValue);
            return true;
        }

        if (targetType == typeof(Guid) && Guid.TryParse(value, out var guidValue))
        {
            writer.WriteStringValue(guidValue);
            return true;
        }

        if (targetType.IsEnum)
        {
            // Enums are stored in the hash as their serialized form (a numeric string like "2" under the
            // default converter, or a member name like "Active" when the caller registered a string enum
            // converter). Emitting a raw JSON string token here would break the default converter, which
            // reads enums from number tokens only. Parse the stored text back into the enum (Enum.TryParse
            // accepts both numeric strings and member names, case-insensitively) and re-serialize through
            // the caller's options so whichever enum converter they use round-trips its own wire format.
            if (Enum.TryParse(targetType, value, ignoreCase: true, out var parsed))
            {
                JsonSerializer.Serialize(writer, parsed, targetType, serializerOptions);
                return true;
            }

            // Preserve prior behavior for values that are neither a defined member nor numeric: emit the
            // string and let the converter throw a descriptive JsonException.
            writer.WriteStringValue(value);
            return true;
        }

        if (TryDeserializeRawJson(value, targetType, serializerOptions, out var deserialized))
        {
            JsonSerializer.Serialize(writer, deserialized, targetType, serializerOptions);
            return true;
        }

        return false;
    }

    private static bool TryDeserializeRawJson(
        string value,
        Type propertyType,
        JsonSerializerOptions serializerOptions,
        out object? deserialized)
    {
        try
        {
            deserialized = JsonSerializer.Deserialize(value, propertyType, serializerOptions);
            return deserialized is not null;
        }
        catch (JsonException)
        {
            deserialized = null;
            return false;
        }
    }

    private static IReadOnlyDictionary<string, Type> GetPropertyTypes(Type documentType, JsonSerializerOptions serializerOptions) =>
        PropertyTypeCache.GetOrAdd(
            new PropertyTypeCacheKey(documentType, serializerOptions),
            static key => BuildPropertyTypes(key.DocumentType, key.SerializerOptions));

    private static IReadOnlyDictionary<string, Type> BuildPropertyTypes(Type documentType, JsonSerializerOptions serializerOptions)
    {
        var properties = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in documentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? serializerOptions.PropertyNamingPolicy?.ConvertName(property.Name)
                ?? property.Name;

            properties[jsonName] = property.PropertyType;
        }

        return properties;
    }

    // JsonSerializerOptions uses reference equality, so callers reusing the same options instance
    // (the common case) share a cache entry; ad-hoc options instances get their own.
    private readonly record struct PropertyTypeCacheKey(Type DocumentType, JsonSerializerOptions SerializerOptions);
}
