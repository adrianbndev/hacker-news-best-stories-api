using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;

namespace HackerNews.Infrastructure.Storage;

public sealed class InMemoryStoryStoreAdapter : IStoryStore
{
    private IReadOnlyList<Story> _snapshot = [];

    public IReadOnlyList<Story> GetSnapshot() => Volatile.Read(ref _snapshot);

    public void Swap(IReadOnlyList<Story> stories) => Interlocked.Exchange(ref _snapshot, stories);
}
