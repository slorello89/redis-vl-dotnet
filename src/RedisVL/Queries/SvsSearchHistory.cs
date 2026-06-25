namespace RedisVL.Queries;

/// <summary>
/// Controls the SVS-VAMANA <c>USE_SEARCH_HISTORY</c> query-time behavior.
/// </summary>
public enum SvsSearchHistory
{
    /// <summary>Let the engine decide whether to use the search history buffer (default).</summary>
    Auto = 0,

    /// <summary>Always use the full search history.</summary>
    On = 1,

    /// <summary>Never use the search history buffer.</summary>
    Off = 2
}
