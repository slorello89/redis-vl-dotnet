namespace RedisVL.Tests.Rerankers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class OnnxIntegrationFactAttribute : FactAttribute
{
    public OnnxIntegrationFactAttribute()
    {
        Skip = OnnxTestEnvironment.SkipReason;
    }
}
