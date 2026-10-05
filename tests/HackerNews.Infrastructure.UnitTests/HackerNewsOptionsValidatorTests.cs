using HackerNews.Infrastructure.HackerNews;

namespace HackerNews.Infrastructure.UnitTests;

public class HackerNewsOptionsValidatorTests
{
    private readonly HackerNewsOptionsValidator _validator = new();

    public static TheoryData<HackerNewsOptions, string> InvalidOptions() => new()
    {
        { new HackerNewsOptions { BaseUrl = "not-a-url" }, nameof(HackerNewsOptions.BaseUrl) },
        { new HackerNewsOptions { BaseUrl = "https://host/v0/?x=1" }, nameof(HackerNewsOptions.BaseUrl) },
        { new HackerNewsOptions { BaseUrl = "https://host/v0/#f" }, nameof(HackerNewsOptions.BaseUrl) },
        { new HackerNewsOptions { RefreshIntervalSeconds = 0 }, nameof(HackerNewsOptions.RefreshIntervalSeconds) },
        { new HackerNewsOptions { RefreshIntervalSeconds = HackerNewsResilienceLimits.MaximumRefreshIntervalSeconds + 1 }, nameof(HackerNewsOptions.RefreshIntervalSeconds) },
        { new HackerNewsOptions { MaxConcurrency = 0 }, nameof(HackerNewsOptions.MaxConcurrency) },
        { new HackerNewsOptions { MaxStories = 0 }, nameof(HackerNewsOptions.MaxStories) },
        { new HackerNewsOptions { MaxStories = -1 }, nameof(HackerNewsOptions.MaxStories) },
        { new HackerNewsOptions { TimeoutSeconds = 0 }, nameof(HackerNewsOptions.TimeoutSeconds) },
        { new HackerNewsOptions { TimeoutSeconds = HackerNewsResilienceLimits.MaximumTimeoutSeconds + 1 }, nameof(HackerNewsOptions.TimeoutSeconds) },
        { new HackerNewsOptions { RetryCount = -1 }, nameof(HackerNewsOptions.RetryCount) },
        { new HackerNewsOptions { RetryCount = HackerNewsResilienceLimits.MaximumRetryCount + 1 }, nameof(HackerNewsOptions.RetryCount) },
        { new HackerNewsOptions { RetryBaseDelayMilliseconds = -1 }, nameof(HackerNewsOptions.RetryBaseDelayMilliseconds) },
        { new HackerNewsOptions { RetryBaseDelayMilliseconds = HackerNewsResilienceLimits.MaximumRetryBaseDelayMilliseconds + 1 }, nameof(HackerNewsOptions.RetryBaseDelayMilliseconds) },
        { new HackerNewsOptions { CircuitBreakerFailures = HackerNewsResilienceLimits.MinimumCircuitBreakerFailures - 1 }, nameof(HackerNewsOptions.CircuitBreakerFailures) },
        { new HackerNewsOptions { CircuitBreakerSeconds = 0 }, nameof(HackerNewsOptions.CircuitBreakerSeconds) },
        { new HackerNewsOptions { CircuitBreakerSeconds = HackerNewsResilienceLimits.MaximumCircuitBreakerSeconds + 1 }, nameof(HackerNewsOptions.CircuitBreakerSeconds) },
    };

    [Fact]
    public void Accepts_the_default_configuration()
    {
        var result = _validator.Validate(null, new HackerNewsOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("https://host/v0/")]
    [InlineData("https://host/v0")]
    public void Accepts_a_base_url_without_query_or_fragment(string baseUrl)
    {
        var result = _validator.Validate(null, new HackerNewsOptions { BaseUrl = baseUrl });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Accepts_the_supported_polly_boundaries()
    {
        var options = new HackerNewsOptions
        {
            BaseUrl = "https://news.ycombinator.com/",
            RefreshIntervalSeconds = HackerNewsResilienceLimits.MaximumRefreshIntervalSeconds,
            RetryCount = HackerNewsResilienceLimits.MaximumRetryCount,
            RetryBaseDelayMilliseconds = HackerNewsResilienceLimits.MaximumRetryBaseDelayMilliseconds,
            CircuitBreakerFailures = HackerNewsResilienceLimits.MinimumCircuitBreakerFailures,
            CircuitBreakerSeconds = HackerNewsResilienceLimits.MaximumCircuitBreakerSeconds,
            TimeoutSeconds = HackerNewsResilienceLimits.MaximumTimeoutSeconds,
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Accepts_a_max_stories_without_upper_bound()
    {
        var result = _validator.Validate(null, new HackerNewsOptions { MaxStories = 100_000 });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public void Rejects_an_invalid_field(HackerNewsOptions options, string expectedField)
    {
        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains(expectedField));
    }

    [Fact]
    public void Reports_every_invalid_field_at_once()
    {
        var options = new HackerNewsOptions
        {
            MaxConcurrency = 0,
            MaxStories = -5,
            CircuitBreakerSeconds = 0,
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(3, result.Failures!.Count());
    }
}
