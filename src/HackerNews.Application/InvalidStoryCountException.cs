using HackerNews.Domain;

namespace HackerNews.Application;

public sealed class InvalidStoryCountException : ArgumentOutOfRangeException
{
    public InvalidStoryCountException(int requestedCount)
        : base(null, $"The requested story count must be greater than or equal to {BestStoriesLimits.MinimumCount}.")
    {
        RequestedCount = requestedCount;
    }

    public int RequestedCount { get; }
}
