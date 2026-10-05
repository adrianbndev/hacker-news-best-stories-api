using HackerNews.Infrastructure.HackerNews;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.Refresh;

public sealed class BestStoriesRefreshService : BackgroundService
{
    private readonly BestStoriesRefresher _refresher;
    private readonly HackerNewsOptions _options;

    public BestStoriesRefreshService(BestStoriesRefresher refresher, IOptions<HackerNewsOptions> options)
    {
        _refresher = refresher;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.RefreshIntervalSeconds));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await _refresher.RefreshAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
