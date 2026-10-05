namespace HackerNews.Infrastructure.HackerNews;

public static class HackerNewsResilienceLimits
{
    public const int MinimumCircuitBreakerFailures = 2;

    public const int MinimumTimeoutSeconds = 1;

    public const int MaximumTimeoutSeconds = 86399;

    public const int MaximumRetryCount = 10;

    public const int MaximumRetryBaseDelayMilliseconds = 86400000;

    public const int MaximumCircuitBreakerSeconds = 86400;

    public const int MaximumRefreshIntervalSeconds = 86400;
}
