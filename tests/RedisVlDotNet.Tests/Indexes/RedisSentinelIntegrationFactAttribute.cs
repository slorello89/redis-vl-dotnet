namespace RedisVlDotNet.Tests.Indexes;

internal sealed class RedisSentinelIntegrationFactAttribute : FactAttribute
{
    public RedisSentinelIntegrationFactAttribute()
    {
        Skip = RedisSentinelTestEnvironment.SkipReason;
    }
}
