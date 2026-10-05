namespace HackerNews.Application;

public sealed class BestStoriesNotReadyException : Exception
{
    public BestStoriesNotReadyException()
        : base("The best stories snapshot is not ready yet.")
    {
    }
}
