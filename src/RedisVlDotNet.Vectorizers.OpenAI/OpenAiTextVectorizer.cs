using System.ClientModel;
using OpenAI;
using OpenAI.Embeddings;
using RedisVlDotNet.Vectorizers;

namespace RedisVlDotNet.Vectorizers.OpenAI;

public sealed class OpenAiTextVectorizer : IBatchTextVectorizer
{
    private readonly EmbeddingClient _client;
    private readonly OpenAiVectorizerOptions _options;

    public OpenAiTextVectorizer(EmbeddingClient client, OpenAiVectorizerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _options = options ?? new OpenAiVectorizerOptions();
    }

    public OpenAiTextVectorizer(string model, string apiKey, OpenAiVectorizerOptions? options = null)
        : this(new EmbeddingClient(model, apiKey), options)
    {
    }

    public OpenAiTextVectorizer(
        string model,
        ApiKeyCredential credential,
        OpenAIClientOptions? clientOptions = null,
        OpenAiVectorizerOptions? options = null)
        : this(new EmbeddingClient(model, credential, clientOptions ?? new OpenAIClientOptions()), options)
    {
    }

    public EmbeddingClient Client => _client;

    public OpenAiVectorizerOptions Options => _options;

    public async Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        ValidateInput(input);

        var response = await _client.GenerateEmbeddingAsync(
            input,
            CreateEmbeddingGenerationOptions(),
            cancellationToken).ConfigureAwait(false);

        return response.Value.ToFloats().ToArray();
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

        var response = await _client.GenerateEmbeddingsAsync(
            normalizedInputs,
            CreateEmbeddingGenerationOptions(),
            cancellationToken).ConfigureAwait(false);

        if (response.Value.Count != normalizedInputs.Length)
        {
            throw new InvalidOperationException("OpenAI embeddings response count did not match the number of requested inputs.");
        }

        return response.Value.Select(static embedding => embedding.ToFloats().ToArray()).ToArray();
    }

    private EmbeddingGenerationOptions CreateEmbeddingGenerationOptions()
    {
        return new EmbeddingGenerationOptions
        {
            Dimensions = _options.Dimensions,
            EndUserId = _options.EndUserId
        };
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
}
