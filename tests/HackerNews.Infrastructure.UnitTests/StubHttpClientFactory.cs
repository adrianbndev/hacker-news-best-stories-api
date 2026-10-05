namespace HackerNews.Infrastructure.UnitTests;

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    private readonly Uri _baseAddress;

    public StubHttpClientFactory(HttpMessageHandler handler, string baseAddress = "https://hacker-news.test/v0/")
    {
        _handler = handler;
        _baseAddress = new Uri(baseAddress);
    }

    public HttpClient CreateClient(string name) => new(_handler) { BaseAddress = _baseAddress };
}
