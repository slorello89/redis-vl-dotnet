namespace RedisVlDotNet.Tests.Vectorizers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class HuggingFaceIntegrationFactAttribute : FactAttribute
{
    public HuggingFaceIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(HuggingFaceTestEnvironment.ApiKey))
        {
            Skip = "Set HF_TOKEN to run Hugging Face smoke tests.";
        }
    }
}
