namespace RedisVl.Tests.Indexes;

internal sealed class RedisSearchIntegrationFactAttribute : FactAttribute
{
    public RedisSearchIntegrationFactAttribute()
    {
        Skip = RedisSearchTestEnvironment.SkipReason;
    }
}
