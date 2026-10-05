using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace HackerNews.Infrastructure.HackerNews;

public static class HackerNewsResiliencePipeline
{
    public static ResiliencePipeline<HttpResponseMessage> Create(HackerNewsOptions options)
    {
        var handledResponses = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(response => (int)response.StatusCode >= 500);

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        if (options.RetryCount > 0)
        {
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = handledResponses,
                MaxRetryAttempts = options.RetryCount,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMilliseconds),
            });
        }

        return builder
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = handledResponses,
                FailureRatio = 1.0,
                MinimumThroughput = options.CircuitBreakerFailures,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreakerSeconds),
                BreakDuration = TimeSpan.FromSeconds(options.CircuitBreakerSeconds),
            })
            .AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds))
            .Build();
    }
}
