namespace HackerNews.Domain;

public static class StoryRanking
{
    public static IReadOnlyList<Story> OrderByScoreDescending(IEnumerable<Story> stories) =>
        stories.OrderByDescending(story => story.Score).ToList();
}
