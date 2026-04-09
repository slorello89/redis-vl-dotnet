using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using StackExchange.Redis;

namespace RedisVlDotNet.Queries;

internal static class SearchResultMapper
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly NullabilityInfoContext NullabilityContext = new();

    public static TDocument Map<TDocument>(SearchDocument document, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var options = serializerOptions ?? DefaultSerializerOptions;
        var metadata = GetPropertyMetadata(typeof(TDocument), options);
        EnsureRequiredFields(document, metadata);

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        foreach (var property in metadata)
        {
            if (!TryGetDocumentValue(document, property, out var value))
            {
                continue;
            }

            writer.WritePropertyName(property.JsonName);
            WriteRedisValue(writer, property, value, options);
        }

        writer.WriteEndObject();
        writer.Flush();

        try
        {
            return JsonSerializer.Deserialize<TDocument>(stream.ToArray(), options)
                ?? throw new SearchResultMappingException(typeof(TDocument), document.Id, "Search result materialized to null.");
        }
        catch (SearchResultMappingException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new SearchResultMappingException(
                typeof(TDocument),
                document.Id,
                $"Failed to materialize search result into '{typeof(TDocument).Name}'.",
                exception);
        }
    }

    private static void EnsureRequiredFields(SearchDocument document, IReadOnlyList<PropertyMetadata> properties)
    {
        foreach (var property in properties)
        {
            if (!property.IsRequired || TryGetDocumentValue(document, property, out _))
            {
                continue;
            }

            throw new SearchResultMappingException(
                property.PropertyType,
                document.Id,
                $"Required field '{property.DisplayName}' was not present in the search result for document '{document.Id}'.");
        }
    }

    private static bool TryGetDocumentValue(SearchDocument document, PropertyMetadata property, out RedisValue value)
    {
        foreach (var candidate in property.SourceNames)
        {
            if (string.Equals(candidate, "id", StringComparison.OrdinalIgnoreCase))
            {
                value = document.Id;
                return true;
            }

            if (document.Values.TryGetValue(candidate, out value))
            {
                return true;
            }
        }

        value = RedisValue.Null;
        return false;
    }

    private static void WriteRedisValue(
        Utf8JsonWriter writer,
        PropertyMetadata property,
        RedisValue value,
        JsonSerializerOptions serializerOptions)
    {
        if (value.IsNull)
        {
            writer.WriteNullValue();
            return;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (targetType == typeof(string))
        {
            writer.WriteStringValue(value.ToString());
            return;
        }

        if (targetType == typeof(byte[]))
        {
            var bytes = (byte[]?)value ?? throw new SearchResultMappingException(
                property.PropertyType,
                null,
                $"Field '{property.DisplayName}' did not contain a binary payload.");
            writer.WriteBase64StringValue(bytes);
            return;
        }

        if (targetType == typeof(bool) && bool.TryParse(value.ToString(), out var boolValue))
        {
            writer.WriteBooleanValue(boolValue);
            return;
        }

        if (targetType == typeof(int) && int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            writer.WriteNumberValue(intValue);
            return;
        }

        if (targetType == typeof(long) && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            writer.WriteNumberValue(longValue);
            return;
        }

        if (targetType == typeof(float) && float.TryParse(value.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
        {
            writer.WriteNumberValue(floatValue);
            return;
        }

        if (targetType == typeof(double) && double.TryParse(value.ToString(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
        {
            writer.WriteNumberValue(doubleValue);
            return;
        }

        if (targetType == typeof(decimal) && decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            writer.WriteNumberValue(decimalValue);
            return;
        }

        if (targetType == typeof(Guid) && Guid.TryParse(value.ToString(), out var guidValue))
        {
            writer.WriteStringValue(guidValue);
            return;
        }

        if (targetType.IsEnum)
        {
            writer.WriteStringValue(value.ToString());
            return;
        }

        if (TryWriteVectorValue(writer, targetType, value))
        {
            return;
        }

        if (TryDeserializeRawJson(value.ToString(), targetType, serializerOptions, out var deserialized))
        {
            JsonSerializer.Serialize(writer, deserialized, targetType, serializerOptions);
            return;
        }

        throw new SearchResultMappingException(
            property.PropertyType,
            null,
            $"Field '{property.DisplayName}' could not be mapped into '{property.PropertyType.Name}'.");
    }

    private static bool TryWriteVectorValue(Utf8JsonWriter writer, Type targetType, RedisValue value)
    {
        var bytes = (byte[]?)value;
        if (bytes is null)
        {
            return false;
        }

        if (targetType == typeof(float[]))
        {
            if (bytes.Length % sizeof(float) != 0)
            {
                return false;
            }

            var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
            JsonSerializer.Serialize(writer, floats);
            return true;
        }

        if (targetType == typeof(double[]))
        {
            if (bytes.Length % sizeof(double) != 0)
            {
                return false;
            }

            var doubles = MemoryMarshal.Cast<byte, double>(bytes.AsSpan()).ToArray();
            JsonSerializer.Serialize(writer, doubles);
            return true;
        }

        return false;
    }

    private static bool TryDeserializeRawJson(
        string? value,
        Type propertyType,
        JsonSerializerOptions serializerOptions,
        out object? deserialized)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            deserialized = null;
            return false;
        }

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

    private static IReadOnlyList<PropertyMetadata> GetPropertyMetadata(Type documentType, JsonSerializerOptions serializerOptions)
    {
        var properties = new List<PropertyMetadata>();
        foreach (var property in documentType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? serializerOptions.PropertyNamingPolicy?.ConvertName(property.Name)
                ?? property.Name;
            var sourceNames = new HashSet<string>(StringComparer.Ordinal)
            {
                jsonName,
                property.Name
            };

            properties.Add(
                new PropertyMetadata(
                    property.PropertyType,
                    property.Name,
                    jsonName,
                    [.. sourceNames],
                    IsRequired(property)));
        }

        return properties;
    }

    private static bool IsRequired(PropertyInfo property)
    {
        var nullability = NullabilityContext.Create(property);
        if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null)
        {
            return true;
        }

        return nullability.WriteState == NullabilityState.NotNull || nullability.ReadState == NullabilityState.NotNull;
    }

    private sealed record PropertyMetadata(
        Type PropertyType,
        string DisplayName,
        string JsonName,
        IReadOnlyList<string> SourceNames,
        bool IsRequired);
}

public sealed class SearchResultMappingException : InvalidOperationException
{
    internal SearchResultMappingException(Type targetType, string? documentId, string message, Exception? innerException = null)
        : base(BuildMessage(targetType, documentId, message), innerException)
    {
        TargetType = targetType;
        DocumentId = documentId;
    }

    public Type TargetType { get; }

    public string? DocumentId { get; }

    private static string BuildMessage(Type targetType, string? documentId, string message)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            return $"{message} Target type: '{targetType.Name}'.";
        }

        return $"{message} Target type: '{targetType.Name}'. Document id: '{documentId}'.";
    }
}
