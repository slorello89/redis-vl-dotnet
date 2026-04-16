using RedisVL.Filters;

namespace RedisVL.Queries;

internal static class QueryReturnFieldHelper
{
    public static IReadOnlyList<string> NormalizeReturnFields(IEnumerable<string>? returnFields, string requiredField)
    {
        var normalized = QueryFieldNormalizer.NormalizeReturnFields(returnFields).ToList();
        var seen = new HashSet<string>(normalized, StringComparer.Ordinal);
        var normalizedRequiredField = FilterExpression.NormalizeFieldName(requiredField);

        if (seen.Add(normalizedRequiredField))
        {
            normalized.Add(normalizedRequiredField);
        }

        return normalized;
    }
}
