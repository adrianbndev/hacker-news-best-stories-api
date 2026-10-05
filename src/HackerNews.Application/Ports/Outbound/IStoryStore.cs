using HackerNews.Domain;

namespace HackerNews.Application.Ports.Outbound;

public interface IStoryStore
{
    IReadOnlyList<Story> GetSnapshot();

    void Swap(IReadOnlyList<Story> stories);
}
