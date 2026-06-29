namespace RedisVL.Tests.Rerankers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class VoyageAiIntegrationFactAttribute : FactAttribute
{
    public VoyageAiIntegrationFactAttribute()
    {
        Skip = VoyageAiTestEnvironment.SkipReason;
    }
}
