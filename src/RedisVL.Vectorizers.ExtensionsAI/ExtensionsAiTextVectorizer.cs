using Microsoft.Extensions.AI;
using RedisVL.Vectorizers;

namespace RedisVL.Vectorizers.ExtensionsAI;

/// <summary>
/// Adapts a Microsoft.Extensions.AI embedding generator to the RedisVL vectorizer contracts.
/// </summary>
public sealed class ExtensionsAiTextVectorizer : IBatchTextVectorizer, IDisposable
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _generator;
    private readonly ExtensionsAiVectorizerOptions _options;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtensionsAiTextVectorizer" /> class.
    /// </summary>
    /// <param name="generator">The wrapped Microsoft.Extensions.AI embedding generator.</param>
    /// <param name="options">Optional request-forwarding settings.</param>
    public ExtensionsAiTextVectorizer(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        ExtensionsAiVectorizerOptions? options = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options ?? new ExtensionsAiVectorizerOptions();
    }

    /// <summary>
    /// Gets the wrapped Microsoft.Extensions.AI embedding generator.
    /// </summary>
    public IEmbeddingGenerator<string, Embedding<float>> Generator => _generator;

    /// <summary>
    /// Gets the adapter configuration.
    /// </summary>
    public ExtensionsAiVectorizerOptions Options => _options;

    /// <inheritdoc />
    public async Task<float[]> VectorizeAsync(string input, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedInput = ValidateInput(input);
        var vector = await _generator.GenerateVectorAsync(
            normalizedInput,
            _options.GenerationOptions,
            cancellationToken).ConfigureAwait(false);

        return vector.ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<float[]>> VectorizeAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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

        var generatedEmbeddings = await _generator.GenerateAsync(
            normalizedInputs,
            _options.GenerationOptions,
            cancellationToken).ConfigureAwait(false);

        if (generatedEmbeddings.Count != normalizedInputs.Length)
        {
            throw new InvalidOperationException(
                "Microsoft.Extensions.AI embeddings response count did not match the number of requested inputs.");
        }

        return generatedEmbeddings.Select(static embedding => embedding.Vector.ToArray()).ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_options.DisposeGenerator)
        {
            _generator.Dispose();
        }

        _disposed = true;
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
