using HackerNews.Domain;
using HackerNews.Infrastructure.Storage;

namespace HackerNews.Infrastructure.UnitTests;

public class InMemoryStoryStoreAdapterTests
{
    [Fact]
    public void Starts_with_an_empty_snapshot()
    {
        var store = new InMemoryStoryStoreAdapter();

        Assert.Empty(store.GetSnapshot());
    }

    [Fact]
    public void Swap_replaces_the_snapshot()
    {
        var store = new InMemoryStoryStoreAdapter();
        var stories = new[] { CreateStory(10) };

        store.Swap(stories);

        Assert.Same(stories, store.GetSnapshot());
    }

    private static Story CreateStory(int score) =>
        new("Story", "https://example.test", "author", DateTimeOffset.UnixEpoch, score, 0);
}
