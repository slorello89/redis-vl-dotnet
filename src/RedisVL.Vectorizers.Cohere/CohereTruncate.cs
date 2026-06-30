namespace RedisVL.Vectorizers.Cohere;

/// <summary>
/// Controls how Cohere handles inputs that exceed the maximum token length.
/// </summary>
public enum CohereTruncate
{
    /// <summary>Return an error when an input exceeds the maximum token length.</summary>
    None,

    /// <summary>Discard the start of the input until it fits within the maximum token length.</summary>
    Start,

    /// <summary>Discard the end of the input until it fits within the maximum token length.</summary>
    End
}
