using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedisVlDotNet.Vectorizers;

namespace RedisVlDotNet.Vectorizers.HuggingFace;

public sealed class HuggingFaceTextVectorizer : IBatchTextVectorizer
{
    private const string DefaultEndpointPrefix = "https://router.huggingface.co/hf-inference/models/";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HuggingFaceVectorizerOptions _options;

    public HuggingFaceTextVectorizer(
        string model,
        string apiKey,
        HuggingFaceVectorizerOptions? options = null,
        HttpClient? httpClient = null)
    {
        _model = ValidateRequired(model, nameof(model));
        _apiKey = ValidateRequired(apiKey, nameof(apiKey));
        _options = options ?? new HuggingFaceVectorizerOptions();
        _httpClient = httpClient ?? new HttpClient();
    }

    public HttpClient Client => _httpClient;

    public string Model => _model;

    public HuggingFaceVectorizerOptions Options => _options;

    public async Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        var normalizedInput = ValidateInput(input);
        var request = CreateRequest([normalizedInput]);
        var embeddings = await SendAsync(request, expectedCount: 1, cancellationToken).ConfigureAwait(false);
        return embeddings[0];
    }

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

        var request = CreateRequest(normalizedInputs);
        return await SendAsync(request, normalizedInputs.Length, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(IReadOnlyList<string> inputs)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BuildEndpoint());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var payload = new FeatureExtractionRequest(
            inputs.Count == 1 ? inputs[0] : inputs,
            _options.Normalize,
            _options.PromptName,
            _options.Truncate,
            ToApiValue(_options.TruncationDirection));

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
                $"Hugging Face feature extraction request failed with status code {(int)response.StatusCode}: {responseBody}");
        }

        return ParseEmbeddings(responseBody, expectedCount);
    }

    private string BuildEndpoint()
    {
        if (!string.IsNullOrWhiteSpace(_options.EndpointOverride))
        {
            return _options.EndpointOverride;
        }

        return $"{DefaultEndpointPrefix}{_model}";
    }

    private static IReadOnlyList<float[]> ParseEmbeddings(string responseBody, int expectedCount)
    {
        using var document = JsonDocument.Parse(responseBody);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Hugging Face feature extraction response must be a JSON array.");
        }

        if (document.RootElement.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Hugging Face feature extraction response was empty.");
        }

        var firstElement = document.RootElement[0];
        if (firstElement.ValueKind == JsonValueKind.Number)
        {
            if (expectedCount != 1)
            {
                throw new InvalidOperationException("Hugging Face embeddings response count did not match the number of requested inputs.");
            }

            return [ParseEmbedding(document.RootElement)];
        }

        if (firstElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Hugging Face feature extraction response must contain arrays of numbers.");
        }

        var embeddings = new List<float[]>(document.RootElement.GetArrayLength());
        foreach (var element in document.RootElement.EnumerateArray())
        {
            embeddings.Add(ParseEmbedding(element));
        }

        if (embeddings.Count != expectedCount)
        {
            throw new InvalidOperationException("Hugging Face embeddings response count did not match the number of requested inputs.");
        }

        return embeddings;
    }

    private static float[] ParseEmbedding(JsonElement embeddingElement)
    {
        var embedding = new float[embeddingElement.GetArrayLength()];
        var index = 0;
        foreach (var value in embeddingElement.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException("Hugging Face embedding values must be numeric.");
            }

            embedding[index++] = value.GetSingle();
        }

        return embedding;
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

    private static string? ToApiValue(HuggingFaceTruncationDirection? direction) =>
        direction switch
        {
            HuggingFaceTruncationDirection.Left => "left",
            HuggingFaceTruncationDirection.Right => "right",
            null => null,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

    private sealed record FeatureExtractionRequest(
        [property: JsonPropertyName("inputs")] object Inputs,
        [property: JsonPropertyName("normalize"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Normalize,
        [property: JsonPropertyName("prompt_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PromptName,
        [property: JsonPropertyName("truncate"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Truncate,
        [property: JsonPropertyName("truncation_direction"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TruncationDirection);
}
