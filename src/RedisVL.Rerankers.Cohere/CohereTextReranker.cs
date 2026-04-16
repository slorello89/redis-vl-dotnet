using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedisVL.Rerankers;

namespace RedisVL.Rerankers.Cohere;

public sealed class CohereTextReranker : ITextReranker
{
    private const string DefaultEndpoint = "https://api.cohere.com/v2/rerank";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly CohereRerankerOptions _options;

    public CohereTextReranker(
        string model,
        string apiKey,
        CohereRerankerOptions? options = null,
        HttpClient? httpClient = null)
    {
        _model = ValidateRequired(model, nameof(model));
        _apiKey = ValidateRequired(apiKey, nameof(apiKey));
        _options = options ?? new CohereRerankerOptions();
        _httpClient = httpClient ?? new HttpClient();
    }

    public HttpClient Client => _httpClient;

    public string Model => _model;

    public CohereRerankerOptions Options => _options;

    public async Task<IReadOnlyList<RerankResult>> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Documents.Count == 0)
        {
            return [];
        }

        using var httpRequest = CreateRequest(request);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cohere rerank request failed with status code {(int)response.StatusCode}: {responseBody}");
        }

        return ParseResults(request, responseBody);
    }

    private HttpRequestMessage CreateRequest(RerankRequest request)
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint());
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        if (!string.IsNullOrWhiteSpace(_options.ClientName))
        {
            httpRequest.Headers.Add("X-Client-Name", _options.ClientName);
        }

        var payload = new CohereRerankRequest(
            _model,
            request.Query,
            request.Documents.Select(static document => document.Text).ToArray(),
            request.TopN,
            _options.MaxTokensPerDocument,
            _options.Priority);

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return httpRequest;
    }

    private string BuildEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(_options.EndpointOverride))
        {
            return _options.EndpointOverride;
        }

        return DefaultEndpoint;
    }

    private static IReadOnlyList<RerankResult> ParseResults(RerankRequest request, string responseBody)
    {
        var response = JsonSerializer.Deserialize<CohereRerankResponse>(responseBody, SerializerOptions)
            ?? throw new InvalidOperationException("Cohere rerank response was empty.");

        if (response.Results is null)
        {
            throw new InvalidOperationException("Cohere rerank response did not contain results.");
        }

        var results = new RerankResult[response.Results.Count];
        for (var index = 0; index < response.Results.Count; index++)
        {
            var result = response.Results[index];
            if (result.Index < 0 || result.Index >= request.Documents.Count)
            {
                throw new InvalidOperationException("Cohere rerank response contained an out-of-range document index.");
            }

            results[index] = new RerankResult(
                result.Index,
                result.RelevanceScore,
                request.Documents[result.Index]);
        }

        return results;
    }

    private static string ValidateRequired(string value, string paramName)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} must be non-empty.", paramName);
        }

        return value;
    }

    private sealed record CohereRerankRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("query")] string Query,
        [property: JsonPropertyName("documents")] IReadOnlyList<string> Documents,
        [property: JsonPropertyName("top_n"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TopN,
        [property: JsonPropertyName("max_tokens_per_doc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxTokensPerDocument,
        [property: JsonPropertyName("priority"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Priority);

    private sealed record CohereRerankResponse(
        [property: JsonPropertyName("results")] IReadOnlyList<CohereRerankResponseItem>? Results);

    private sealed record CohereRerankResponseItem(
        [property: JsonPropertyName("index")] int Index,
        [property: JsonPropertyName("relevance_score")] double RelevanceScore);
}
