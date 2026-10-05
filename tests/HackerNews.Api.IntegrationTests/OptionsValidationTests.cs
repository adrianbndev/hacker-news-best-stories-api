using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HackerNews.Api.IntegrationTests;

public class OptionsValidationTests
{
    [Fact]
    public async Task Fails_to_start_when_refresh_interval_is_not_positive()
    {
        await AssertStartupFailsAsync("HackerNews:RefreshIntervalSeconds", "0");
    }

    [Fact]
    public async Task Fails_to_start_when_refresh_interval_exceeds_a_full_day()
    {
        await AssertStartupFailsAsync("HackerNews:RefreshIntervalSeconds", "86401");
    }

    [Fact]
    public async Task Fails_to_start_when_max_concurrency_is_not_positive()
    {
        await AssertStartupFailsAsync("HackerNews:MaxConcurrency", "0");
    }

    [Fact]
    public async Task Fails_to_start_when_circuit_breaker_failures_is_below_the_polly_minimum()
    {
        await AssertStartupFailsAsync("HackerNews:CircuitBreakerFailures", "1");
    }

    [Fact]
    public async Task Fails_to_start_when_timeout_reaches_a_full_day()
    {
        await AssertStartupFailsAsync("HackerNews:TimeoutSeconds", "86400");
    }

    [Fact]
    public async Task Fails_to_start_when_retry_base_delay_exceeds_a_full_day()
    {
        await AssertStartupFailsAsync("HackerNews:RetryBaseDelayMilliseconds", "86400001");
    }

    [Fact]
    public async Task Fails_to_start_when_circuit_breaker_seconds_exceed_a_full_day()
    {
        await AssertStartupFailsAsync("HackerNews:CircuitBreakerSeconds", "86401");
    }

    [Fact]
    public async Task Fails_to_start_when_retry_count_exceeds_the_maximum()
    {
        await AssertStartupFailsAsync("HackerNews:RetryCount", "11");
    }

    [Fact]
    public async Task Fails_to_start_when_base_url_is_not_absolute_http()
    {
        await AssertStartupFailsAsync("HackerNews:BaseUrl", "not-a-url");
    }

    [Fact]
    public async Task Starts_with_a_valid_configuration()
    {
        using var host = BuildHost(new Dictionary<string, string?>());

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public async Task Starts_with_the_supported_upper_boundaries()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["HackerNews:BaseUrl"] = "http://localhost:8080/v0",
            ["HackerNews:CircuitBreakerFailures"] = "2",
            ["HackerNews:CircuitBreakerSeconds"] = "86400",
            ["HackerNews:RefreshIntervalSeconds"] = "86400",
            ["HackerNews:RetryBaseDelayMilliseconds"] = "86400000",
            ["HackerNews:RetryCount"] = "10",
            ["HackerNews:TimeoutSeconds"] = "86399",
        });

        await host.StartAsync();
        await host.StopAsync();
    }

    private static async Task AssertStartupFailsAsync(string key, string value)
    {
        using var host = BuildHost(new Dictionary<string, string?> { [key] = value });

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    private static IHost BuildHost(IReadOnlyDictionary<string, string?> overrides)
    {
        var settings = new Dictionary<string, string?>
        {
            ["HackerNews:BaseUrl"] = "https://hacker-news.test/v0/",
            ["HackerNews:RefreshIntervalSeconds"] = "3600",
            ["HackerNews:MaxConcurrency"] = "16",
            ["HackerNews:TimeoutSeconds"] = "10",
            ["HackerNews:RetryCount"] = "3",
            ["HackerNews:RetryBaseDelayMilliseconds"] = "200",
            ["HackerNews:CircuitBreakerFailures"] = "5",
            ["HackerNews:CircuitBreakerSeconds"] = "30",
        };

        foreach (var (key, value) in overrides)
        {
            settings[key] = value;
        }

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddHackerNewsOptions(builder.Configuration);
        return builder.Build();
    }
}
