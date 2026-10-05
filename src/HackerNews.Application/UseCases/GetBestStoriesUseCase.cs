using HackerNews.Application.Ports.Inbound;
using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;

namespace HackerNews.Application.UseCases;

public sealed class GetBestStoriesUseCase : IGetBestStoriesUseCase
{
    private readonly IStoryStore _storyStore;

    public GetBestStoriesUseCase(IStoryStore storyStore) => _storyStore = storyStore;

    public Task<IReadOnlyList<Story>> ExecuteAsync(int n, CancellationToken ct)
    {
        if (n < BestStoriesLimits.MinimumCount)
        {
            throw new InvalidStoryCountException(n);
        }

        var snapshot = _storyStore.GetSnapshot();

        if (snapshot.Count == 0)
        {
            throw new BestStoriesNotReadyException();
        }

        return Task.FromResult<IReadOnlyList<Story>>(snapshot.Take(n).ToList());
    }
}
