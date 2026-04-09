using RedisVlDotNet.Filters;

namespace RedisVlDotNet.Queries;

internal static class QueryFieldNormalizer
{
    public static IReadOnlyList<string> NormalizeReturnFields(IEnumerable<string>? returnFields)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (returnFields is null)
        {
            return normalized;
        }

        foreach (var field in returnFields)
        {
            var normalizedField = FilterExpression.NormalizeFieldName(field);
            if (seen.Add(normalizedField))
            {
                normalized.Add(normalizedField);
            }
        }

        return normalized;
    }
}
