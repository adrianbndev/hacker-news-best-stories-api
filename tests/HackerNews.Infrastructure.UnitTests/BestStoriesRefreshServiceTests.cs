using HackerNews.Domain;
using HackerNews.Infrastructure.HackerNews;
using HackerNews.Infrastructure.Refresh;
using HackerNews.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.UnitTests;

public class BestStoriesRefreshServiceTests
{
    [Fact]
    public async Task Does_not_refresh_before_the_first_timer_tick()
    {
        var store = new InMemoryStoryStoreAdapter();
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = [1],
            StoryFactory = id => CreateStory(id, score: id),
        };
        var options = new HackerNewsOptions { RefreshIntervalSeconds = HackerNewsResilienceLimits.MaximumRefreshIntervalSeconds };

        var service = CreateService(client, store, options);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Empty(store.GetSnapshot());
        Assert.False(client.BestStoryIdsCalled.Task.IsCompleted);
    }

    [Fact]
    public async Task Refreshes_on_the_first_timer_tick()
    {
        var store = new InMemoryStoryStoreAdapter();
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = [1],
            StoryFactory = id => CreateStory(id, score: id),
        };
        var options = new HackerNewsOptions { RefreshIntervalSeconds = 1 };

        var service = CreateService(client, store, options);

        await service.StartAsync(CancellationToken.None);
        await WaitUntilSnapshotIsPopulatedAsync(store);
        await service.StopAsync(CancellationToken.None);

        Assert.Single(store.GetSnapshot());
    }

    [Fact]
    public async Task Keeps_the_previous_snapshot_when_the_background_refresh_fails()
    {
        var store = new InMemoryStoryStoreAdapter();
        store.Swap([CreateStory(99, score: 100)]);
        var client = new FakeHackerNewsClient { BestStoryIdsFailure = new HttpRequestException("hacker news is down") };
        var options = new HackerNewsOptions { RefreshIntervalSeconds = 1 };

        var service = CreateService(client, store, options);

        await service.StartAsync(CancellationToken.None);
        await client.BestStoryIdsCalled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        var snapshot = store.GetSnapshot();
        Assert.Single(snapshot);
        Assert.Equal(100, snapshot[0].Score);
    }

    private static BestStoriesRefreshService CreateService(
        FakeHackerNewsClient client,
        InMemoryStoryStoreAdapter store,
        HackerNewsOptions options) =>
        new(
            new BestStoriesRefresher(client, store, Options.Create(options), NullLogger<BestStoriesRefresher>.Instance),
            Options.Create(options));

    private static async Task WaitUntilSnapshotIsPopulatedAsync(InMemoryStoryStoreAdapter store)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);

        while (store.GetSnapshot().Count == 0)
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("The best stories snapshot was not populated within 5 seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }

    private static Story CreateStory(int id, int score) =>
        new($"Story {id}", $"https://example.test/{id}", "author", DateTimeOffset.UnixEpoch, score, id);
}
