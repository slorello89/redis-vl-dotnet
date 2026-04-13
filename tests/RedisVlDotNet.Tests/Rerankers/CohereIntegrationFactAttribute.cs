namespace RedisVlDotNet.Tests.Rerankers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class CohereIntegrationFactAttribute : FactAttribute
{
    public CohereIntegrationFactAttribute()
    {
        Skip = CohereTestEnvironment.SkipReason;
    }
}
