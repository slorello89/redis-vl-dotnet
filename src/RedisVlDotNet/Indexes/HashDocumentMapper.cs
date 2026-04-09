using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace RedisVlDotNet.Indexes;

internal static class HashDocumentMapper
{
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

    private static Dictionary<string, Type> GetPropertyTypes(Type documentType, JsonSerializerOptions serializerOptions)
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
}
