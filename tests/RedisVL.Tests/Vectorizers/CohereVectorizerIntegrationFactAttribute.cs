namespace RedisVL.Tests.Vectorizers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class CohereVectorizerIntegrationFactAttribute : FactAttribute
{
    public CohereVectorizerIntegrationFactAttribute()
    {
        Skip = CohereVectorizerTestEnvironment.SkipReason;
    }
}
