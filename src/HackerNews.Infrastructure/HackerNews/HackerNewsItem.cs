namespace HackerNews.Infrastructure.HackerNews;

internal sealed class HackerNewsItem
{
    public string? Type { get; init; }

    public string? By { get; init; }

    public long? Time { get; init; }

    public string? Title { get; init; }

    public string? Url { get; init; }

    public int? Score { get; init; }

    public int? Descendants { get; init; }
}
