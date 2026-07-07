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
        : this(text, returnFields, offset, limit, pagination, fieldWeights, raw: false)
    {
    }

    private TextQuery(
        string text,
        IEnumerable<string>? returnFields,
        int offset,
        int limit,
        QueryPagination? pagination,
        IReadOnlyDictionary<string, double>? fieldWeights,
        bool raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Text = text.Trim();
        IsRaw = raw;
        Pagination = pagination ?? new QueryPagination(offset, limit);
        Offset = Pagination.Offset;
        Limit = Pagination.Limit;
        ReturnFields = QueryFieldNormalizer.NormalizeReturnFields(returnFields);
        FieldWeights = NormalizeFieldWeights(fieldWeights);
    }

    /// <summary>
    /// Creates a query whose <paramref name="text"/> is sent to RediSearch verbatim, bypassing the
    /// escaping applied by the standard constructor. Use this only when the text is a trusted,
    /// hand-authored RediSearch query expression (field filters, boolean operators, wildcards, etc.).
    /// </summary>
    /// <remarks>
    /// Do not pass unsanitized end-user input here: because the FT.SEARCH query is itself a query
    /// language, raw text can change query semantics (match-all with <c>*</c>, probing fields the app
    /// never exposed via <c>@field:</c>, negation with <c>-term</c>) or trigger a server-side syntax
    /// error. The standard <see cref="TextQuery(string, IEnumerable{string}?, int, int, QueryPagination?, IReadOnlyDictionary{string, double}?)"/>
    /// constructor escapes such text and is the correct choice for search-box input.
    /// </remarks>
    public static TextQuery Raw(
        string text,
        IEnumerable<string>? returnFields = null,
        int offset = 0,
        int limit = 10,
        QueryPagination? pagination = null) =>
        new(text, returnFields, offset, limit, pagination, fieldWeights: null, raw: true);

    public string Text { get; }

    /// <summary>
    /// When <see langword="true"/>, <see cref="Text"/> is sent to RediSearch verbatim; otherwise the
    /// text is tokenized and each term is escaped before being sent as the query. Set via <see cref="Raw"/>.
    /// </summary>
    public bool IsRaw { get; }

    public int Offset { get; }

    public int Limit { get; }

    public QueryPagination Pagination { get; }

    public IReadOnlyList<string> ReturnFields { get; }

    /// <summary>
    /// Per-field search weights, in declaration order. Empty when no weights were supplied, in which case
    /// the escaped terms are searched across all fields.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, double>> FieldWeights { get; }

    /// <summary>
    /// The RediSearch query string. When <see cref="IsRaw"/> is <see langword="true"/> this is the raw
    /// <see cref="Text"/>. Otherwise the text is tokenized and each term is escaped: with no
    /// <see cref="FieldWeights"/> the escaped terms are OR-combined across all searchable fields, and with
    /// weights they are spread across the weighted fields.
    /// </summary>
    public string QueryString =>
        IsRaw ? Text
        : FieldWeights.Count == 0 ? BuildEscapedTerms()
        : BuildWeightedQuery();

    // Clones the query with different pagination while preserving Text, ReturnFields, the ordered
    // FieldWeights, and the raw flag — batch paging must not silently drop weights or re-escape raw text.
    private TextQuery(TextQuery source, QueryPagination pagination)
    {
        Text = source.Text;
        IsRaw = source.IsRaw;
        Pagination = pagination;
        Offset = pagination.Offset;
        Limit = pagination.Limit;
        ReturnFields = source.ReturnFields;
        FieldWeights = source.FieldWeights;
    }

    internal TextQuery WithPagination(QueryPagination pagination) => new(this, pagination);

    private string BuildEscapedTerms() =>
        string.Join(
            " | ",
            Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(FilterExpression.EscapeTextTerm));

    private string BuildWeightedQuery()
    {
        var terms = BuildEscapedTerms();

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
