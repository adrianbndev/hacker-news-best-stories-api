using HackerNews.Domain;

namespace HackerNews.Application.UnitTests;

public class StoryRankingTests
{
    [Fact]
    public void Orders_stories_by_score_descending()
    {
        var stories = new[] { CreateStory(score: 10), CreateStory(score: 99), CreateStory(score: 42) };

        var ordered = StoryRanking.OrderByScoreDescending(stories);

        Assert.Equal(new[] { 99, 42, 10 }, ordered.Select(story => story.Score));
    }

    private static Story CreateStory(int score) =>
        new(
            Title: $"Story {score}",
            Uri: $"https://example.test/{score}",
            PostedBy: "author",
            Time: DateTimeOffset.UnixEpoch,
            Score: score,
            CommentCount: 0);
}
