namespace RedisVL.Caches;

public readonly record struct EmbeddingsCacheLookup
{
    public EmbeddingsCacheLookup(string input, string? modelName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        Input = input;
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName;
    }

    public string Input { get; }

    public string? ModelName { get; }
}
