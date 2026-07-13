using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedisVL.Vectorizers;

namespace RedisVL.Vectorizers.Cohere;

/// <summary>
/// An <see cref="IBatchTextVectorizer"/> that generates text embeddings using Cohere's embed API.
/// </summary>
public sealed class CohereTextVectorizer : IBatchTextVectorizer
{
    private const string DefaultEndpoint = "https://api.cohere.com/v2/embed";
    private const string FloatEmbeddingType = "float";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyList<string> FloatEmbeddingTypes = [FloatEmbeddingType];

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly CohereVectorizerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CohereTextVectorizer"/> class.
    /// </summary>
    /// <param name="model">The Cohere embedding model name (for example, <c>embed-english-v3.0</c>).</param>
    /// <param name="apiKey">The Cohere API key used as the bearer credential.</param>
    /// <param name="options">Optional embedding options such as input type, output dimension, and truncation.</param>
    /// <param name="httpClient">An optional <see cref="HttpClient"/>; a new instance is created when not supplied.</param>
    public CohereTextVectorizer(
        string model,
        string apiKey,
        CohereVectorizerOptions? options = null,
        HttpClient? httpClient = null)
    {
        _model = ValidateRequired(model, nameof(model));
        _apiKey = ValidateRequired(apiKey, nameof(apiKey));
        _options = options ?? new CohereVectorizerOptions();
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>Gets the <see cref="HttpClient"/> used to call the Cohere API.</summary>
    public HttpClient Client => _httpClient;

    /// <summary>Gets the Cohere embedding model name.</summary>
    public string Model => _model;

    /// <summary>Gets the embedding options applied to each request.</summary>
    public CohereVectorizerOptions Options => _options;

    /// <inheritdoc/>
    public async Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalizedInput = ValidateInput(input);
        using var request = CreateRequest([normalizedInput]);
        var embeddings = await SendAsync(request, expectedCount: 1, cancellationToken).ConfigureAwait(false);
        return embeddings[0];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<float[]>> VectorizeAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return [];
        }

        var normalizedInputs = new string[inputs.Count];
        for (var index = 0; index < inputs.Count; index++)
        {
            normalizedInputs[index] = ValidateInput(inputs[index], index);
        }

        using var request = CreateRequest(normalizedInputs);
        return await SendAsync(request, normalizedInputs.Length, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(IReadOnlyList<string> inputs)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        if (!string.IsNullOrWhiteSpace(_options.ClientName))
        {
            request.Headers.Add("X-Client-Name", _options.ClientName);
        }

        var payload = new CohereEmbedRequest(
            _model,
            inputs,
            ToApiValue(_options.InputType),
            FloatEmbeddingTypes,
            _options.OutputDimension,
            ToApiValue(_options.Truncate));

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, SerializerOptions),
            Encoding.UTF8,
            "application/json");

        return request;
    }

    private async Task<IReadOnlyList<float[]>> SendAsync(
        HttpRequestMessage request,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Cohere embed request failed with status code {(int)response.StatusCode}: {responseBody}");
        }

        return ParseEmbeddings(responseBody, expectedCount);
    }

    private string BuildEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(_options.EndpointOverride))
        {
            return _options.EndpointOverride;
        }

        return DefaultEndpoint;
    }

    private static IReadOnlyList<float[]> ParseEmbeddings(string responseBody, int expectedCount)
    {
        var response = JsonSerializer.Deserialize<CohereEmbedResponse>(responseBody, SerializerOptions)
            ?? throw new InvalidOperationException("Cohere embed response was empty.");

        var embeddings = response.Embeddings?.Float;
        if (embeddings is null)
        {
            throw new InvalidOperationException("Cohere embed response did not contain float embeddings.");
        }

        if (embeddings.Count != expectedCount)
        {
            throw new InvalidOperationException("Cohere embeddings response count did not match the number of requested inputs.");
        }

        return embeddings;
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

    private static string ValidateInput(string input, int? index = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length == 0)
        {
            throw new ArgumentException(
                index.HasValue
                    ? $"Embedding input at index {index.Value} must be non-empty."
                    : "Embedding input must be non-empty.",
                nameof(input));
        }

        return input;
    }

    private static string ToApiValue(CohereInputType inputType) =>
        inputType switch
        {
            CohereInputType.SearchDocument => "search_document",
            CohereInputType.SearchQuery => "search_query",
            CohereInputType.Classification => "classification",
            CohereInputType.Clustering => "clustering",
            _ => throw new ArgumentOutOfRangeException(nameof(inputType))
        };

    private static string? ToApiValue(CohereTruncate? truncate) =>
        truncate switch
        {
            CohereTruncate.None => "NONE",
            CohereTruncate.Start => "START",
            CohereTruncate.End => "END",
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(truncate))
        };

    private sealed record CohereEmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("texts")] IReadOnlyList<string> Texts,
        [property: JsonPropertyName("input_type")] string InputType,
        [property: JsonPropertyName("embedding_types")] IReadOnlyList<string> EmbeddingTypes,
        [property: JsonPropertyName("output_dimension"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? OutputDimension,
        [property: JsonPropertyName("truncate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Truncate);

    private sealed record CohereEmbedResponse(
        [property: JsonPropertyName("embeddings")] CohereEmbeddings? Embeddings);

    private sealed record CohereEmbeddings(
        [property: JsonPropertyName("float")] IReadOnlyList<float[]>? Float);
}
