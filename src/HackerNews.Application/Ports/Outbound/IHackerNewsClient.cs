using HackerNews.Domain;

namespace HackerNews.Application.Ports.Outbound;

public interface IHackerNewsClient
{
    Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct);

    Task<Story?> GetItemAsync(int id, CancellationToken ct);
}
