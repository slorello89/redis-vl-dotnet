using System.Text;
using RedisVL.Filters;

namespace RedisVL.Queries;

/// <summary>
/// A full-text query over an <c>FT.SEARCH</c> index, optionally spreading its terms across multiple fields
/// with per-field weights to bias relevance scoring.
/// </summary>
public sealed class TextQuery
{
    /// <summary>
    /// Initializes a new <see cref="TextQuery"/>.
    /// </summary>
    /// <param name="text">The search text; must be non-empty.</param>
    /// <param name="returnFields">The fields to return for each match; when <see langword="null"/> all fields are returned.</param>
    /// <param name="offset">The number of leading results to skip.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <param name="pagination">Optional pagination window; overrides <paramref name="offset"/> and <paramref name="limit"/> when supplied.</param>
    /// <param name="fieldWeights">Optional per-field search weights; each weight must be greater than zero.</param>
    /// <exception cref="ArgumentException">A weight in <paramref name="fieldWeights"/> is not greater than zero.</exception>
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

    /// <summary>The trimmed search text supplied by the caller.</summary>
    public string Text { get; }

    /// <summary>The number of leading results to skip.</summary>
    public int Offset { get; }

    /// <summary>The maximum number of results to return.</summary>
    public int Limit { get; }

    /// <summary>The pagination window applied to the results.</summary>
    public QueryPagination Pagination { get; }

    /// <summary>The fields returned for each matching document.</summary>
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
