namespace HackerNews.Domain;

public sealed record Story(
    string Title,
    string Uri,
    string PostedBy,
    DateTimeOffset Time,
    int Score,
    int CommentCount);
