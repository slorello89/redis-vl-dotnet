using System.Reflection;
using RedisVL.Schema;

namespace RedisVL.Indexes;

internal static class DocumentKeyResolver
{
    public static string ResolveKey<TDocument>(SearchSchema schema, TDocument document, string? key = null, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(document);

        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Only one of key or id can be provided.");
        }

        if (!string.IsNullOrWhiteSpace(key))
        {
            return key.Trim();
        }

        var resolvedId = !string.IsNullOrWhiteSpace(id)
            ? id.Trim()
            : TryGetId(document) ?? throw new ArgumentException(
                "Document key could not be resolved. Provide an explicit key, an explicit id, or a document with an Id property.",
                nameof(document));

        return $"{schema.Index.Prefix}{resolvedId}";
    }

    public static string ResolveKeyForSelectors<TDocument>(
        SearchSchema schema,
        TDocument document,
        Func<TDocument, string>? keySelector = null,
        Func<TDocument, string>? idSelector = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(document);

        if (keySelector is not null && idSelector is not null)
        {
            throw new ArgumentException("Only one of keySelector or idSelector can be provided.");
        }

        if (keySelector is not null)
        {
            var explicitKey = keySelector(document);
            return string.IsNullOrWhiteSpace(explicitKey)
                ? throw new ArgumentException("Resolved document key cannot be blank.", nameof(keySelector))
                : explicitKey.Trim();
        }

        if (idSelector is not null)
        {
            var explicitId = idSelector(document);
            if (string.IsNullOrWhiteSpace(explicitId))
            {
                throw new ArgumentException("Resolved document id cannot be blank.", nameof(idSelector));
            }

            return $"{schema.Index.Prefix}{explicitId.Trim()}";
        }

        return ResolveKey(schema, document, key: null, id: null);
    }

    public static string ResolveKeyFromId(SearchSchema schema, string id)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return $"{schema.Index.Prefix}{id.Trim()}";
    }

    private static string? TryGetId<TDocument>(TDocument document)
    {
        var property = document!.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(static candidate =>
                string.Equals(candidate.Name, "id", StringComparison.OrdinalIgnoreCase) &&
                candidate.CanRead &&
                candidate.GetIndexParameters().Length == 0);

        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(document);
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            string stringValue when !string.IsNullOrWhiteSpace(stringValue) => stringValue.Trim(),
            _ => value.ToString()
        };
    }
}
