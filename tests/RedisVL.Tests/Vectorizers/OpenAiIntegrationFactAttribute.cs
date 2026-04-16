namespace RedisVL.Tests.Vectorizers;

internal sealed class OpenAiIntegrationFactAttribute : FactAttribute
{
    public OpenAiIntegrationFactAttribute()
    {
        Skip = OpenAiTestEnvironment.SkipReason;
    }
}
