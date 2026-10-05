using System.ComponentModel.DataAnnotations;

namespace HackerNews.Infrastructure.HackerNews;

public sealed class HackerNewsOptions
{
    public string BaseUrl { get; set; } = "https://hacker-news.firebaseio.com/v0/";

    [Range(1, HackerNewsResilienceLimits.MaximumRefreshIntervalSeconds)]
    public int RefreshIntervalSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int MaxConcurrency { get; set; } = 16;

    [Range(1, int.MaxValue)]
    public int? MaxStories { get; set; }

    [Range(HackerNewsResilienceLimits.MinimumTimeoutSeconds, HackerNewsResilienceLimits.MaximumTimeoutSeconds)]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(0, HackerNewsResilienceLimits.MaximumRetryCount)]
    public int RetryCount { get; set; } = 3;

    [Range(0, HackerNewsResilienceLimits.MaximumRetryBaseDelayMilliseconds)]
    public int RetryBaseDelayMilliseconds { get; set; } = 200;

    [Range(HackerNewsResilienceLimits.MinimumCircuitBreakerFailures, int.MaxValue)]
    public int CircuitBreakerFailures { get; set; } = 5;

    [Range(1, HackerNewsResilienceLimits.MaximumCircuitBreakerSeconds)]
    public int CircuitBreakerSeconds { get; set; } = 30;
}
