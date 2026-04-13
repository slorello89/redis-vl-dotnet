namespace RedisVlDotNet.Tests.Indexes;

internal sealed class RedisClusterIntegrationFactAttribute : FactAttribute
{
    public RedisClusterIntegrationFactAttribute()
    {
        Skip = RedisClusterTestEnvironment.SkipReason;
    }
}
