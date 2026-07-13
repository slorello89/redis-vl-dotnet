namespace RedisVL.Tests.Indexes;

/// <summary>
/// Marks an integration test that requires the native <c>FT.HYBRID</c> command (Redis 8.4+). Skips
/// when no Redis connection is configured (like <see cref="RedisSearchIntegrationFactAttribute" />)
/// and, additionally, when the connected server is too old to implement <c>FT.HYBRID</c> — so the
/// test capability-skips on a pre-8.4 server instead of hard-failing.
/// </summary>
internal sealed class RedisNativeHybridSearchIntegrationFactAttribute : FactAttribute
{
    public RedisNativeHybridSearchIntegrationFactAttribute()
    {
        Skip = RedisSearchTestEnvironment.SkipReason ?? RedisSearchCapabilities.NativeHybridSkipReason;
    }
}
