using Microsoft.Extensions.AI;

namespace RedisVL.Vectorizers.ExtensionsAI;

/// <summary>
/// Configures how the Microsoft.Extensions.AI adapter forwards embedding requests.
/// </summary>
public sealed class ExtensionsAiVectorizerOptions
{
    /// <summary>
    /// Gets the optional embedding-generation options forwarded to the wrapped generator.
    /// </summary>
    public EmbeddingGenerationOptions? GenerationOptions { get; init; }

    /// <summary>
    /// Gets whether disposing the adapter also disposes the wrapped generator.
    /// </summary>
    public bool DisposeGenerator { get; init; }
}
