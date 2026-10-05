using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;

namespace HackerNews.Infrastructure.UnitTests;

internal sealed class FakeHackerNewsClient : IHackerNewsClient
{
    private int _itemCalls;
    private int _currentConcurrency;
    private int _maxObservedConcurrency;

    public IReadOnlyList<int> BestStoryIds { get; set; } = [];

    public Func<int, Story?> StoryFactory { get; set; } = _ => null;

    public Exception? BestStoryIdsFailure { get; set; }

    public Func<int, bool> ItemFailure { get; set; } = _ => false;

    public TimeSpan ItemDelay { get; set; }

    public TaskCompletionSource BestStoryIdsCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int ItemCalls => Volatile.Read(ref _itemCalls);

    public int MaxObservedConcurrency => Volatile.Read(ref _maxObservedConcurrency);

    public Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct)
    {
        BestStoryIdsCalled.TrySetResult();

        if (BestStoryIdsFailure is not null)
        {
            throw BestStoryIdsFailure;
        }

        return Task.FromResult(BestStoryIds);
    }

    public async Task<Story?> GetItemAsync(int id, CancellationToken ct)
    {
        Interlocked.Increment(ref _itemCalls);
        var concurrency = Interlocked.Increment(ref _currentConcurrency);
        UpdateMaxObservedConcurrency(concurrency);

        try
        {
            if (ItemDelay > TimeSpan.Zero)
            {
                await Task.Delay(ItemDelay, ct);
            }

            if (ItemFailure(id))
            {
                throw new HttpRequestException($"Could not fetch Hacker News item {id}.");
            }

            return StoryFactory(id);
        }
        finally
        {
            Interlocked.Decrement(ref _currentConcurrency);
        }
    }

    private void UpdateMaxObservedConcurrency(int value)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref _maxObservedConcurrency);
            if (value <= observed)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _maxObservedConcurrency, value, observed) != observed);
    }
}
