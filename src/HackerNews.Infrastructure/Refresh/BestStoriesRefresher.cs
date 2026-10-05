using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;
using HackerNews.Infrastructure.HackerNews;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.Refresh;

public sealed class BestStoriesRefresher
{
    private readonly IHackerNewsClient _hackerNewsClient;
    private readonly IStoryStore _storyStore;
    private readonly HackerNewsOptions _options;
    private readonly ILogger<BestStoriesRefresher> _logger;

    public BestStoriesRefresher(
        IHackerNewsClient hackerNewsClient,
        IStoryStore storyStore,
        IOptions<HackerNewsOptions> options,
        ILogger<BestStoriesRefresher> logger)
    {
        _hackerNewsClient = hackerNewsClient;
        _storyStore = storyStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var bestStoryIds = await _hackerNewsClient.GetBestStoryIdsAsync(ct);
            var selectedIds = _options.MaxStories is int maxStories ? bestStoryIds.Take(maxStories) : bestStoryIds;
            var stories = await FetchStoriesAsync(selectedIds, ct);

            if (stories.Count == 0)
            {
                _logger.LogWarning("Refreshing the best stories snapshot returned no stories; keeping the previous snapshot.");
                return;
            }

            _storyStore.Swap(StoryRanking.OrderByScoreDescending(stories));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Refreshing the best stories snapshot failed; keeping the previous snapshot.");
        }
    }

    private async Task<IReadOnlyList<Story>> FetchStoriesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        using var throttle = new SemaphoreSlim(_options.MaxConcurrency);

        var fetches = ids.Select(id => FetchStoryAsync(id, throttle, ct));
        var stories = await Task.WhenAll(fetches);
        return stories.Where(story => story is not null).Select(story => story!).ToList();
    }

    private async Task<Story?> FetchStoryAsync(int id, SemaphoreSlim throttle, CancellationToken ct)
    {
        await throttle.WaitAsync(ct);
        try
        {
            return await _hackerNewsClient.GetItemAsync(id, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Fetching Hacker News item {StoryId} failed; skipping it.", id);
            return null;
        }
        finally
        {
            throttle.Release();
        }
    }
}
