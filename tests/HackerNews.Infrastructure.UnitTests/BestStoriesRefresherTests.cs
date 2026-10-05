using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;
using HackerNews.Infrastructure.HackerNews;
using HackerNews.Infrastructure.Refresh;
using HackerNews.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HackerNews.Infrastructure.UnitTests;

public class BestStoriesRefresherTests
{
    [Fact]
    public async Task Refresh_orders_the_snapshot_by_score_descending()
    {
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = [1, 2, 3],
            StoryFactory = id => CreateStory(id, score: id * 10),
        };
        var store = new InMemoryStoryStoreAdapter();

        await CreateRefresher(client, store).RefreshAsync(CancellationToken.None);

        Assert.Equal(new[] { 30, 20, 10 }, store.GetSnapshot().Select(story => story.Score));
    }

    [Fact]
    public async Task Refresh_keeps_the_previous_snapshot_when_hacker_news_fails()
    {
        var store = new InMemoryStoryStoreAdapter();
        store.Swap([CreateStory(99, score: 100)]);
        var client = new FakeHackerNewsClient { BestStoryIdsFailure = new HttpRequestException("hacker news is down") };

        await CreateRefresher(client, store).RefreshAsync(CancellationToken.None);

        var snapshot = store.GetSnapshot();
        Assert.Single(snapshot);
        Assert.Equal(100, snapshot[0].Score);
    }

    [Fact]
    public async Task Refresh_limits_fetches_when_max_stories_is_set()
    {
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = [1, 2, 3, 4, 5, 6],
            StoryFactory = id => CreateStory(id, id),
        };
        var store = new InMemoryStoryStoreAdapter();
        var options = TestOptions();
        options.MaxStories = 2;

        await CreateRefresher(client, store, options).RefreshAsync(CancellationToken.None);

        Assert.Equal(2, client.ItemCalls);
    }

    [Fact]
    public async Task Refresh_uses_every_id_when_max_stories_is_not_set()
    {
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = [1, 2, 3, 4, 5, 6],
            StoryFactory = id => CreateStory(id, id),
        };
        var store = new InMemoryStoryStoreAdapter();
        var options = TestOptions();
        options.MaxStories = null;

        await CreateRefresher(client, store, options).RefreshAsync(CancellationToken.None);

        Assert.Equal(6, client.ItemCalls);
        Assert.Equal(6, store.GetSnapshot().Count);
    }

    [Fact]
    public async Task Refresh_skips_a_failing_item_and_keeps_the_others()
    {
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = [1, 2, 3],
            StoryFactory = id => CreateStory(id, id),
            ItemFailure = id => id == 2,
        };
        var store = new InMemoryStoryStoreAdapter();

        await CreateRefresher(client, store).RefreshAsync(CancellationToken.None);

        Assert.Equal(new[] { 3, 1 }, store.GetSnapshot().Select(story => story.Score));
    }

    [Fact]
    public async Task Refresh_keeps_the_previous_snapshot_when_every_item_fails()
    {
        var store = new InMemoryStoryStoreAdapter();
        store.Swap([CreateStory(99, score: 100)]);
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = [1, 2, 3],
            StoryFactory = id => CreateStory(id, id),
            ItemFailure = _ => true,
        };

        await CreateRefresher(client, store).RefreshAsync(CancellationToken.None);

        var snapshot = store.GetSnapshot();
        Assert.Single(snapshot);
        Assert.Equal(100, snapshot[0].Score);
    }

    [Fact]
    public async Task Refresh_never_exceeds_the_configured_concurrency()
    {
        var client = new FakeHackerNewsClient
        {
            BestStoryIds = Enumerable.Range(1, 24).ToList(),
            StoryFactory = id => CreateStory(id, id),
            ItemDelay = TimeSpan.FromMilliseconds(20),
        };
        var store = new InMemoryStoryStoreAdapter();
        var options = TestOptions();
        options.MaxConcurrency = 4;

        await CreateRefresher(client, store, options).RefreshAsync(CancellationToken.None);

        Assert.True(
            client.MaxObservedConcurrency <= options.MaxConcurrency,
            $"Observed concurrency {client.MaxObservedConcurrency} exceeded the configured maximum {options.MaxConcurrency}.");
        Assert.Equal(24, store.GetSnapshot().Count);
    }

    private static BestStoriesRefresher CreateRefresher(
        IHackerNewsClient client,
        IStoryStore store,
        HackerNewsOptions? options = null) =>
        new(client, store, Options.Create(options ?? TestOptions()), NullLogger<BestStoriesRefresher>.Instance);

    private static HackerNewsOptions TestOptions() => new()
    {
        RefreshIntervalSeconds = 3600,
        MaxConcurrency = 8,
        TimeoutSeconds = 30,
        RetryCount = 0,
        CircuitBreakerFailures = 1000,
        CircuitBreakerSeconds = 30,
    };

    private static Story CreateStory(int id, int score) =>
        new($"Story {id}", $"https://example.test/{id}", "author", DateTimeOffset.UnixEpoch, score, id);
}
