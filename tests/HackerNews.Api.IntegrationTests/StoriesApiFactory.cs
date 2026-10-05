using HackerNews.Application.Ports.Outbound;
using HackerNews.Infrastructure.Refresh;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace HackerNews.Api.IntegrationTests;

public sealed class StoriesApiFactory : WebApplicationFactory<Program>
{
    public StubHackerNewsClient HackerNewsClient { get; } = new();

    public bool DisableBackgroundRefresh { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HackerNews:BaseUrl"] = "https://hacker-news.test/v0/",
                ["HackerNews:RefreshIntervalSeconds"] = "3600",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHackerNewsClient>();
            services.AddSingleton<IHackerNewsClient>(HackerNewsClient);

            if (DisableBackgroundRefresh)
            {
                var backgroundRefresh = services
                    .Where(descriptor =>
                        descriptor.ServiceType == typeof(IHostedService) &&
                        descriptor.ImplementationType == typeof(BestStoriesRefreshService))
                    .ToList();

                foreach (var descriptor in backgroundRefresh)
                {
                    services.Remove(descriptor);
                }
            }
        });
    }
}
