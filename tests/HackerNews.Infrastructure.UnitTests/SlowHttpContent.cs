using System.Net;

namespace HackerNews.Infrastructure.UnitTests;

internal sealed class SlowHttpContent : HttpContent
{
    private readonly TimeSpan _delay;

    public SlowHttpContent(TimeSpan delay) => _delay = delay;

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        SerializeSlowlyAsync(stream, CancellationToken.None);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
        SerializeSlowlyAsync(stream, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        length = 2;
        return true;
    }

    private async Task SerializeSlowlyAsync(Stream stream, CancellationToken ct)
    {
        await Task.Delay(_delay, ct);
        await stream.WriteAsync("[]"u8.ToArray(), ct);
    }
}
