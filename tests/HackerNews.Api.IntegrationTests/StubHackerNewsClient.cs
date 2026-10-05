using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;

namespace HackerNews.Api.IntegrationTests;

public sealed class StubHackerNewsClient : IHackerNewsClient
{
    private int _bestStoryIdsCalls;
    private int _itemCalls;

    public IReadOnlyList<int> BestStoryIds { get; set; } = [];

    public Func<int, Story?> ItemFactory { get; set; } = _ => null;

    public bool FailBestStoryIds { get; set; }

    public int BestStoryIdsCalls => Volatile.Read(ref _bestStoryIdsCalls);

    public int ItemCalls => Volatile.Read(ref _itemCalls);

    public int TotalCalls => BestStoryIdsCalls + ItemCalls;

    public void ResetCounters()
    {
        Interlocked.Exchange(ref _bestStoryIdsCalls, 0);
        Interlocked.Exchange(ref _itemCalls, 0);
    }

    public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _bestStoryIdsCalls);

        if (FailBestStoryIds)
        {
            throw new HttpRequestException("hacker news is unavailable");
        }

        return Task.FromResult(BestStoryIds);
    }

    public Task<Story?> GetItemAsync(int id, CancellationToken ct)
    {
        Interlocked.Increment(ref _itemCalls);
        return Task.FromResult(ItemFactory(id));
    }
}
