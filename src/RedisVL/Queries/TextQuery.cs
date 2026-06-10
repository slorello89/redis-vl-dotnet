using System.Text;
using RedisVL.Filters;

namespace RedisVL.Queries;

public sealed class TextQuery
{
    public TextQuery(
        string text,
        IEnumerable<string>? returnFields = null,
        int offset = 0,
        int limit = 10,
        QueryPagination? pagination = null,
        IReadOnlyDictionary<string, double>? fieldWeights = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Text = text.Trim();
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
        FieldWeights = NormalizeFieldWeights(fieldWeights);
    }

    public string Text { get; }

    public int Offset { get; }

    public int Limit { get; }

    public QueryPagination Pagination { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>
    /// Per-field search weights, in declaration order. Empty when the query uses the raw <see cref="Text"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, double>> FieldWeights { get; }

    /// <summary>
    /// The RediSearch query string. When <see cref="FieldWeights"/> is empty this is the raw <see cref="Text"/>;
    /// otherwise the text terms are spread across the weighted fields.
    /// </summary>
    public string QueryString => FieldWeights.Count == 0 ? Text : BuildWeightedQuery();

    private string BuildWeightedQuery()
    {
        var terms = string.Join(
            " | ",
            Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(FilterExpression.EscapeTextTerm));

        var builder = new StringBuilder();
        for (var i = 0; i < FieldWeights.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(" | ");
            }

            var (field, weight) = FieldWeights[i];
            builder.Append('@').Append(field).Append(":(").Append(terms).Append(')');

            if (weight != 1.0)
            {
                builder.Append(" => { $weight: ").Append(FilterExpression.FormatNumber(weight)).Append(" }");
            }
        }

        return FieldWeights.Count > 1 ? $"({builder})" : builder.ToString();
    }

    private static IReadOnlyList<KeyValuePair<string, double>> NormalizeFieldWeights(
        IReadOnlyDictionary<string, double>? fieldWeights)
    {
        if (fieldWeights is null || fieldWeights.Count == 0)
        {
            return [];
        }

        var normalized = new List<KeyValuePair<string, double>>(fieldWeights.Count);
        foreach (var (field, weight) in fieldWeights)
        {
            if (weight <= 0)
            {
                throw new ArgumentException(
                    $"Weight for field '{field}' must be greater than zero.",
                    nameof(fieldWeights));
            }

            normalized.Add(new KeyValuePair<string, double>(FilterExpression.NormalizeFieldName(field), weight));
        }

        return normalized;
    }
}
