using HackerNews.Domain;

namespace HackerNews.Application.Ports.Inbound;

public interface IGetBestStoriesUseCase
{
    Task<IReadOnlyList<Story>> ExecuteAsync(int n, CancellationToken ct);
}
