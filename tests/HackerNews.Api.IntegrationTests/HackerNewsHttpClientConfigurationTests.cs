using HackerNews.Infrastructure.HackerNews;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.Api.IntegrationTests;

public class HackerNewsHttpClientConfigurationTests
{
    [Fact]
    public void The_named_client_delegates_the_timeout_to_polly()
    {
        using var factory = new StoriesApiFactory { DisableBackgroundRefresh = true };

        var httpClientFactory = factory.Services.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(HackerNewsHttpClientAdapter.HttpClientName);

        Assert.Equal(Timeout.InfiniteTimeSpan, httpClient.Timeout);
    }
}
