using System.Net;
using System.Text;

namespace HackerNews.Infrastructure.UnitTests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _response;
    private int _requestCount;

    public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) =>
        _response = response;

    public int RequestCount => Volatile.Read(ref _requestCount);

    public static StubHttpMessageHandler Json(string json) =>
        new((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, json)));

    public static StubHttpMessageHandler Status(HttpStatusCode status) =>
        new((_, _) => Task.FromResult(JsonResponse(status, "{}")));

    public static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        return _response(request, cancellationToken);
    }
}
