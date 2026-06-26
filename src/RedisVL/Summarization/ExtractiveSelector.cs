using RedisVL.Vectorizers;

namespace RedisVL.Summarization;

/// <summary>
/// Embedding-based extractive summarization. Sentences are embedded with a vectorizer, clustered
/// with k-means++, and the sentence nearest each cluster centroid is selected. Selected sentences
/// are returned in their original order with their exact original text preserved.
/// </summary>
/// <remarks>
/// Extractive (as opposed to abstractive) selection never rewrites text, so named entities, numbers,
/// and quotations survive verbatim — useful for trimming RAG / chat-history context without
/// introducing paraphrase errors.
/// </remarks>
public sealed class ExtractiveSelector
{
    private readonly IBatchTextVectorizer _embedder;
    private readonly int _defaultNumSentences;
    private readonly int _maxIterations;
    private readonly int? _randomSeed;

    /// <summary>Creates an extractive selector.</summary>
    /// <param name="embedder">Vectorizer used to embed sentences in a single batch call.</param>
    /// <param name="defaultNumSentences">Default number of sentences to select when no count is supplied.</param>
    /// <param name="maxIterations">Maximum k-means iterations.</param>
    /// <param name="randomSeed">Optional seed for deterministic clustering. When null, selection is non-deterministic.</param>
    public ExtractiveSelector(
        IBatchTextVectorizer embedder,
        int defaultNumSentences = 10,
        int maxIterations = 100,
        int? randomSeed = null)
    {
        ArgumentNullException.ThrowIfNull(embedder);
        if (defaultNumSentences <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultNumSentences), defaultNumSentences, "Default sentence count must be greater than zero.");
        }

        if (maxIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxIterations), maxIterations, "Maximum iterations must be greater than zero.");
        }

        _embedder = embedder;
        _defaultNumSentences = defaultNumSentences;
        _maxIterations = maxIterations;
        _randomSeed = randomSeed;
    }

    /// <summary>Selects the most representative sentences using the configured default count.</summary>
    public Task<IReadOnlyList<string>> SelectKeySentencesAsync(
        IReadOnlyList<string> sentences,
        CancellationToken cancellationToken = default) =>
        SelectKeySentencesAsync(sentences, _defaultNumSentences, cancellationToken);

    /// <summary>Selects up to <paramref name="k"/> representative sentences, preserving original order and text.</summary>
    /// <param name="sentences">Candidate sentences.</param>
    /// <param name="k">Maximum number of sentences to select.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The selected sentences in their original order. Returns all non-blank sentences when there are
    /// at most <paramref name="k"/> of them. May return fewer than <paramref name="k"/> when the
    /// embeddings collapse into fewer distinct clusters.
    /// </returns>
    public async Task<IReadOnlyList<string>> SelectKeySentencesAsync(
        IReadOnlyList<string> sentences,
        int k,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), k, "Sentence count must be greater than zero.");
        }

        if (sentences.Count == 0)
        {
            return [];
        }

        var valid = new List<(int Index, string Text)>(sentences.Count);
        for (var i = 0; i < sentences.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(sentences[i]))
            {
                valid.Add((i, sentences[i]));
            }
        }

        if (valid.Count == 0)
        {
            return [];
        }

        if (valid.Count <= k)
        {
            return valid.Select(static entry => entry.Text).ToList();
        }

        var texts = valid.Select(static entry => entry.Text).ToList();
        var embeddings = await _embedder.VectorizeAsync(texts, cancellationToken).ConfigureAwait(false);

        if (embeddings.Count != texts.Count)
        {
            throw new InvalidOperationException(
                $"Vectorizer returned {embeddings.Count} embeddings for {texts.Count} sentences.");
        }

        var seed = _randomSeed ?? Random.Shared.Next();
        var representativeLocalIndices = KMeansClustering.SelectRepresentatives(embeddings, k, _maxIterations, seed);

        return representativeLocalIndices
            .Select(local => valid[local].Index)
            .Distinct()
            .OrderBy(static index => index)
            .Select(index => sentences[index])
            .ToList();
    }
}
