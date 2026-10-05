using System.Text.Json;
using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;
using Polly;

namespace HackerNews.Infrastructure.HackerNews;

public sealed class HackerNewsHttpClientAdapter : IHackerNewsClient
{
    public const string HttpClientName = "hacker-news";

    private const long MinimumUnixTimeSeconds = -62135596800;

    private const long MaximumUnixTimeSeconds = 253402300799;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public HackerNewsHttpClientAdapter(IHttpClientFactory httpClientFactory, ResiliencePipeline<HttpResponseMessage> pipeline)
    {
        _httpClientFactory = httpClientFactory;
        _pipeline = pipeline;
    }

    public async Task<IReadOnlyList<int>> GetBestStoryIdsAsync(CancellationToken ct)
    {
        var ids = await GetFromJsonAsync<int[]>("beststories.json", ct);
        return ids ?? [];
    }

    public async Task<Story?> GetItemAsync(int id, CancellationToken ct)
    {
        var item = await GetFromJsonAsync<HackerNewsItem>($"item/{id}.json", ct);
        return MapStory(id, item);
    }

    private async Task<T?> GetFromJsonAsync<T>(string relativePath, CancellationToken ct)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var requestUri = BuildRequestUri(httpClient.BaseAddress, relativePath);
        using var response = await _pipeline.ExecuteAsync(
            token => new ValueTask<HttpResponseMessage>(httpClient.GetAsync(requestUri, token)),
            ct);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    private static Uri BuildRequestUri(Uri? baseAddress, string relativePath)
    {
        if (baseAddress is null)
        {
            throw new InvalidOperationException($"The {HttpClientName} HttpClient has no base address.");
        }

        var normalizedBase = baseAddress.AbsoluteUri.EndsWith('/')
            ? baseAddress
            : new Uri(baseAddress.AbsoluteUri + "/");

        return new Uri(normalizedBase, relativePath);
    }

    private static Story? MapStory(int id, HackerNewsItem? item)
    {
        if (item is null || item.Type != "story" || string.IsNullOrEmpty(item.Title) || item.Score is null)
        {
            return null;
        }

        if (item.Time is not long unixTime || unixTime < MinimumUnixTimeSeconds || unixTime > MaximumUnixTimeSeconds)
        {
            return null;
        }

        return new Story(
            item.Title,
            string.IsNullOrEmpty(item.Url) ? $"https://news.ycombinator.com/item?id={id}" : item.Url,
            item.By ?? string.Empty,
            DateTimeOffset.FromUnixTimeSeconds(unixTime),
            item.Score.Value,
            item.Descendants ?? 0);
    }
}
