using HackerNews.Domain;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.HackerNews;

public sealed class HackerNewsOptionsValidator : IValidateOptions<HackerNewsOptions>
{
    public ValidateOptionsResult Validate(string? name, HackerNewsOptions options)
    {
        var failures = new List<string>();

        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(baseUri.Query) ||
            !string.IsNullOrEmpty(baseUri.Fragment))
        {
            failures.Add($"{nameof(HackerNewsOptions.BaseUrl)} must be an absolute http or https URL without query or fragment.");
        }

        if (options.RefreshIntervalSeconds < 1 ||
            options.RefreshIntervalSeconds > HackerNewsResilienceLimits.MaximumRefreshIntervalSeconds)
        {
            failures.Add($"{nameof(HackerNewsOptions.RefreshIntervalSeconds)} must be between 1 and {HackerNewsResilienceLimits.MaximumRefreshIntervalSeconds}.");
        }

        if (options.MaxConcurrency <= 0)
        {
            failures.Add($"{nameof(HackerNewsOptions.MaxConcurrency)} must be greater than zero.");
        }

        if (options.MaxStories is int maxStories && maxStories < BestStoriesLimits.MinimumCount)
        {
            failures.Add($"{nameof(HackerNewsOptions.MaxStories)} must be greater than or equal to {BestStoriesLimits.MinimumCount} when set.");
        }

        if (options.TimeoutSeconds < HackerNewsResilienceLimits.MinimumTimeoutSeconds ||
            options.TimeoutSeconds > HackerNewsResilienceLimits.MaximumTimeoutSeconds)
        {
            failures.Add($"{nameof(HackerNewsOptions.TimeoutSeconds)} must be between {HackerNewsResilienceLimits.MinimumTimeoutSeconds} and {HackerNewsResilienceLimits.MaximumTimeoutSeconds}.");
        }

        if (options.RetryCount < 0 || options.RetryCount > HackerNewsResilienceLimits.MaximumRetryCount)
        {
            failures.Add($"{nameof(HackerNewsOptions.RetryCount)} must be between 0 and {HackerNewsResilienceLimits.MaximumRetryCount}.");
        }

        if (options.RetryBaseDelayMilliseconds < 0 ||
            options.RetryBaseDelayMilliseconds > HackerNewsResilienceLimits.MaximumRetryBaseDelayMilliseconds)
        {
            failures.Add($"{nameof(HackerNewsOptions.RetryBaseDelayMilliseconds)} must be between 0 and {HackerNewsResilienceLimits.MaximumRetryBaseDelayMilliseconds}.");
        }

        if (options.CircuitBreakerFailures < HackerNewsResilienceLimits.MinimumCircuitBreakerFailures)
        {
            failures.Add($"{nameof(HackerNewsOptions.CircuitBreakerFailures)} must be at least {HackerNewsResilienceLimits.MinimumCircuitBreakerFailures}.");
        }

        if (options.CircuitBreakerSeconds < 1 ||
            options.CircuitBreakerSeconds > HackerNewsResilienceLimits.MaximumCircuitBreakerSeconds)
        {
            failures.Add($"{nameof(HackerNewsOptions.CircuitBreakerSeconds)} must be between 1 and {HackerNewsResilienceLimits.MaximumCircuitBreakerSeconds}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
