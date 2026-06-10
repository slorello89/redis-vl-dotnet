namespace RedisVL.Tests.Vectorizers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class OnnxVectorizerIntegrationFactAttribute : FactAttribute
{
    public OnnxVectorizerIntegrationFactAttribute()
    {
        Skip = OnnxVectorizerTestEnvironment.SkipReason;
    }
}
