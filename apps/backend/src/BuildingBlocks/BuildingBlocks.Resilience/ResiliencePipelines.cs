using Polly;
using Polly.Retry;

namespace IndexDesk.BuildingBlocks.Resilience;

public static class ResiliencePipelines
{
    public static ResiliencePipeline CreateDefaultHttpPipeline(int maxRetries = 3)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(
                new RetryStrategyOptions
                {
                    MaxRetryAttempts = maxRetries,
                    Delay = TimeSpan.FromMilliseconds(500),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                }
            )
            .AddTimeout(TimeSpan.FromSeconds(10))
            .Build();
    }
}
