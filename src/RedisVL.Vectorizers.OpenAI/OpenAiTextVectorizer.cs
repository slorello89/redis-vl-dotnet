using System.ClientModel;
using OpenAI;
using OpenAI.Embeddings;
using RedisVL.Vectorizers;

namespace RedisVL.Vectorizers.OpenAI;

/// <summary>
/// An <see cref="IBatchTextVectorizer"/> that generates text embeddings using OpenAI's embeddings API.
/// </summary>
public sealed class OpenAiTextVectorizer : IBatchTextVectorizer
{
    private readonly EmbeddingClient _client;
    private readonly OpenAiVectorizerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiTextVectorizer"/> class using a preconfigured
    /// OpenAI <see cref="EmbeddingClient"/>.
    /// </summary>
    /// <param name="client">The OpenAI embedding client used to generate embeddings.</param>
    /// <param name="options">Optional embedding options such as dimensions and end-user identifier.</param>
    public OpenAiTextVectorizer(EmbeddingClient client, OpenAiVectorizerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _options = options ?? new OpenAiVectorizerOptions();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiTextVectorizer"/> class using a model name and API key.
    /// </summary>
    /// <param name="model">The OpenAI embedding model name (for example, <c>text-embedding-3-small</c>).</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="options">Optional embedding options such as dimensions and end-user identifier.</param>
    public OpenAiTextVectorizer(string model, string apiKey, OpenAiVectorizerOptions? options = null)
        : this(new EmbeddingClient(model, apiKey), options)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiTextVectorizer"/> class using a model name and credential.
    /// </summary>
    /// <param name="model">The OpenAI embedding model name (for example, <c>text-embedding-3-small</c>).</param>
    /// <param name="credential">The credential used to authenticate with OpenAI.</param>
    /// <param name="clientOptions">Optional OpenAI client options (for example, a custom endpoint).</param>
    /// <param name="options">Optional embedding options such as dimensions and end-user identifier.</param>
    public OpenAiTextVectorizer(
        string model,
        ApiKeyCredential credential,
        OpenAIClientOptions? clientOptions = null,
        OpenAiVectorizerOptions? options = null)
        : this(new EmbeddingClient(model, credential, clientOptions ?? new OpenAIClientOptions()), options)
    {
    }

    /// <summary>Gets the underlying OpenAI <see cref="EmbeddingClient"/>.</summary>
    public EmbeddingClient Client => _client;

    /// <summary>Gets the embedding options applied to each request.</summary>
    public OpenAiVectorizerOptions Options => _options;

    /// <inheritdoc/>
    public async Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        ValidateInput(input);

        var response = await _client.GenerateEmbeddingAsync(
            input,
            CreateEmbeddingGenerationOptions(),
            cancellationToken).ConfigureAwait(false);

        return response.Value.ToFloats().ToArray();
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
