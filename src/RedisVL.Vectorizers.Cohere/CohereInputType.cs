namespace RedisVL.Vectorizers.Cohere;

/// <summary>
/// Identifies the intended use of the inputs so Cohere can prepend the matching task prefix.
/// </summary>
public enum CohereInputType
{
    /// <summary>Embeddings stored in a vector database for retrieval.</summary>
    SearchDocument,

    /// <summary>Embeddings used to query a vector database.</summary>
    SearchQuery,

    /// <summary>Embeddings used as input to a classification task.</summary>
    Classification,

    /// <summary>Embeddings used as input to a clustering task.</summary>
    Clustering
}
