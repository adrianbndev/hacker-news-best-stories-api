using HackerNews.Application;
using HackerNews.Application.Ports.Outbound;
using HackerNews.Application.UseCases;
using HackerNews.Domain;

namespace HackerNews.Application.UnitTests;

public class GetBestStoriesUseCaseTests
{
    [Fact]
    public async Task Throws_when_the_snapshot_is_not_ready()
    {
        var useCase = new GetBestStoriesUseCase(new FakeStoryStore());

        await Assert.ThrowsAsync<BestStoriesNotReadyException>(
            () => useCase.ExecuteAsync(n: 10, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_the_top_n_stories_from_the_ordered_snapshot()
    {
        var store = new FakeStoryStore(CreateStory(50), CreateStory(35), CreateStory(20), CreateStory(5));
        var useCase = new GetBestStoriesUseCase(store);

        var stories = await useCase.ExecuteAsync(n: 2, CancellationToken.None);

        Assert.Equal(new[] { 50, 35 }, stories.Select(story => story.Score));
    }

    [Fact]
    public async Task Returns_every_available_story_when_n_exceeds_the_snapshot()
    {
        var store = new FakeStoryStore(CreateStory(50), CreateStory(5));
        var useCase = new GetBestStoriesUseCase(store);

        var stories = await useCase.ExecuteAsync(n: 10, CancellationToken.None);

        Assert.Equal(new[] { 50, 5 }, stories.Select(story => story.Score));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_count_below_one(int n)
    {
        var useCase = new GetBestStoriesUseCase(new FakeStoryStore(CreateStory(1)));

        await Assert.ThrowsAsync<InvalidStoryCountException>(
            () => useCase.ExecuteAsync(n, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_every_available_story_for_a_very_large_count()
    {
        var store = new FakeStoryStore(CreateStory(50), CreateStory(5));
        var useCase = new GetBestStoriesUseCase(store);

        var stories = await useCase.ExecuteAsync(n: int.MaxValue, CancellationToken.None);

        Assert.Equal(new[] { 50, 5 }, stories.Select(story => story.Score));
    }

    [Fact]
    public void The_invalid_count_message_has_no_parameter_suffix()
    {
        var exception = new InvalidStoryCountException(0);

        Assert.Equal("The requested story count must be greater than or equal to 1.", exception.Message);
        Assert.Equal(0, exception.RequestedCount);
    }

    private static Story CreateStory(int score) =>
        new(
            Title: $"Story {score}",
            Uri: $"https://example.test/{score}",
            PostedBy: "author",
            Time: DateTimeOffset.UnixEpoch,
            Score: score,
            CommentCount: 0);

    private sealed class FakeStoryStore : IStoryStore
    {
        private IReadOnlyList<Story> _snapshot;

        public FakeStoryStore(params Story[] stories) => _snapshot = stories;

        public IReadOnlyList<Story> GetSnapshot() => _snapshot;

        public void Swap(IReadOnlyList<Story> stories) => _snapshot = stories;
    }
}
