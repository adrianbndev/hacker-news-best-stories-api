using HackerNews.Infrastructure.HackerNews;

namespace HackerNews.Infrastructure.UnitTests;

public class HackerNewsResiliencePipelineTests
{
    [Theory]
    [InlineData(HackerNewsResilienceLimits.MinimumCircuitBreakerFailures, HackerNewsResilienceLimits.MinimumTimeoutSeconds, 1)]
    [InlineData(5, HackerNewsResilienceLimits.MaximumTimeoutSeconds, 0)]
    public void Builds_with_the_supported_boundaries(int circuitBreakerFailures, int timeoutSeconds, int retryBaseDelayMilliseconds)
    {
        var options = new HackerNewsOptions
        {
            CircuitBreakerFailures = circuitBreakerFailures,
            TimeoutSeconds = timeoutSeconds,
            RetryCount = 1,
            RetryBaseDelayMilliseconds = retryBaseDelayMilliseconds,
            CircuitBreakerSeconds = 30,
        };

        var pipeline = HackerNewsResiliencePipeline.Create(options);

        Assert.NotNull(pipeline);
    }

    [Fact]
    public void Builds_with_the_upper_supported_boundaries()
    {
        var options = new HackerNewsOptions
        {
            RetryCount = 3,
            RetryBaseDelayMilliseconds = HackerNewsResilienceLimits.MaximumRetryBaseDelayMilliseconds,
            CircuitBreakerFailures = HackerNewsResilienceLimits.MinimumCircuitBreakerFailures,
            CircuitBreakerSeconds = HackerNewsResilienceLimits.MaximumCircuitBreakerSeconds,
            TimeoutSeconds = HackerNewsResilienceLimits.MaximumTimeoutSeconds,
        };

        var exception = Record.Exception(() => HackerNewsResiliencePipeline.Create(options));

        Assert.Null(exception);
    }
}
