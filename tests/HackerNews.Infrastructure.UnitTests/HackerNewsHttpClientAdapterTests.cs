using System.Net;
using HackerNews.Infrastructure.HackerNews;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace HackerNews.Infrastructure.UnitTests;

public class HackerNewsHttpClientAdapterTests
{
    [Fact]
    public async Task Retries_server_errors_until_the_request_succeeds()
    {
        var statuses = new Queue<HttpStatusCode>(
        [
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK,
        ]);
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(StubHttpMessageHandler.JsonResponse(statuses.Dequeue(), "[1,2,3]")));

        var options = PermissiveOptions();
        options.RetryCount = 3;
        options.RetryBaseDelayMilliseconds = 1;
        var adapter = CreateAdapter(handler, options);

        var ids = await adapter.GetBestStoryIdsAsync(CancellationToken.None);

        Assert.Equal(new[] { 1, 2, 3 }, ids);
        Assert.Equal(3, handler.RequestCount);
    }

    [Fact]
    public async Task Opens_the_circuit_after_consecutive_failures_and_fails_fast()
    {
        var handler = StubHttpMessageHandler.Status(HttpStatusCode.ServiceUnavailable);

        var options = PermissiveOptions();
        options.CircuitBreakerFailures = 2;
        var adapter = CreateAdapter(handler, options);

        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.GetBestStoryIdsAsync(CancellationToken.None));
        await Assert.ThrowsAsync<HttpRequestException>(() => adapter.GetBestStoryIdsAsync(CancellationToken.None));
        await Assert.ThrowsAsync<BrokenCircuitException>(() => adapter.GetBestStoryIdsAsync(CancellationToken.None));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Stops_waiting_when_the_request_takes_too_long()
    {
        var handler = new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return StubHttpMessageHandler.JsonResponse(HttpStatusCode.OK, "[]");
        });

        var options = PermissiveOptions();
        options.TimeoutSeconds = 1;
        var adapter = CreateAdapter(handler, options);

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => adapter.GetBestStoryIdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Stops_waiting_while_the_response_body_is_still_streaming()
    {
        var handler = new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new SlowHttpContent(TimeSpan.FromSeconds(5)),
            }));

        var options = PermissiveOptions();
        options.TimeoutSeconds = 1;
        var adapter = CreateAdapter(handler, options);

        await Assert.ThrowsAsync<TimeoutRejectedException>(() => adapter.GetBestStoryIdsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Returns_no_ids_when_the_best_stories_payload_is_null()
    {
        var adapter = CreateAdapter(StubHttpMessageHandler.Json("null"));

        var ids = await adapter.GetBestStoryIdsAsync(CancellationToken.None);

        Assert.Empty(ids);
    }

    [Fact]
    public async Task Resolves_relative_paths_when_the_base_url_has_no_trailing_slash()
    {
        Uri? requestedUri = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(StubHttpMessageHandler.JsonResponse(HttpStatusCode.OK, "[1,2]"));
        });
        var adapter = CreateAdapter(handler, baseAddress: "https://hacker-news.test/v0");

        var ids = await adapter.GetBestStoryIdsAsync(CancellationToken.None);

        Assert.Equal("https://hacker-news.test/v0/beststories.json", requestedUri?.AbsoluteUri);
        Assert.Equal(new[] { 1, 2 }, ids);
    }

    [Fact]
    public async Task Maps_a_complete_story_item()
    {
        const string json = """
            {"id":1,"type":"story","by":"ismaildonmez","time":1570887781,"title":"A uBlock Origin update was rejected from the Chrome Web Store","url":"https://github.com/uBlockOrigin/uBlock-issues/issues/745","score":1716,"descendants":572}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(1, CancellationToken.None);

        Assert.NotNull(story);
        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", story.Title);
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", story.Uri);
        Assert.Equal("ismaildonmez", story.PostedBy);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1570887781), story.Time);
        Assert.Equal(1716, story.Score);
        Assert.Equal(572, story.CommentCount);
    }

    [Fact]
    public async Task Falls_back_to_the_item_page_when_the_url_is_missing()
    {
        const string json = """
            {"type":"story","by":"author","time":1570887781,"title":"No link","url":null,"score":10}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(42, CancellationToken.None);

        Assert.NotNull(story);
        Assert.Equal("https://news.ycombinator.com/item?id=42", story.Uri);
    }

    [Fact]
    public async Task Falls_back_to_the_item_page_when_the_url_is_empty()
    {
        const string json = """
            {"type":"story","by":"author","time":1570887781,"title":"Empty link","url":"","score":10}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(42, CancellationToken.None);

        Assert.NotNull(story);
        Assert.Equal("https://news.ycombinator.com/item?id=42", story.Uri);
    }

    [Fact]
    public async Task Returns_an_empty_author_when_by_is_missing()
    {
        const string json = """
            {"type":"story","time":1570887781,"title":"Anonymous","url":"https://example.test","score":10}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(1, CancellationToken.None);

        Assert.NotNull(story);
        Assert.Equal(string.Empty, story.PostedBy);
    }

    [Fact]
    public async Task Discards_a_story_with_an_out_of_range_time()
    {
        const string json = """
            {"type":"story","by":"author","time":99999999999999,"title":"From the future","url":"https://example.test","score":10}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(1, CancellationToken.None);

        Assert.Null(story);
    }

    [Fact]
    public async Task Defaults_the_comment_count_to_zero_when_descendants_is_missing()
    {
        const string json = """
            {"type":"story","by":"author","time":1570887781,"title":"Quiet thread","url":"https://example.test","score":10}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(7, CancellationToken.None);

        Assert.NotNull(story);
        Assert.Equal(0, story.CommentCount);
    }

    [Fact]
    public async Task Discards_an_item_that_is_not_a_story()
    {
        const string json = """
            {"type":"job","by":"author","time":1570887781,"title":"We are hiring","score":1}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(1, CancellationToken.None);

        Assert.Null(story);
    }

    [Fact]
    public async Task Discards_a_missing_item()
    {
        var adapter = CreateAdapter(StubHttpMessageHandler.Json("null"));

        var story = await adapter.GetItemAsync(1, CancellationToken.None);

        Assert.Null(story);
    }

    [Fact]
    public async Task Discards_a_story_without_a_title()
    {
        const string json = """
            {"type":"story","by":"author","time":1570887781,"score":10}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(1, CancellationToken.None);

        Assert.Null(story);
    }

    [Fact]
    public async Task Discards_a_story_without_a_score()
    {
        const string json = """
            {"type":"story","by":"author","time":1570887781,"title":"No score"}
            """;
        var adapter = CreateAdapter(StubHttpMessageHandler.Json(json));

        var story = await adapter.GetItemAsync(1, CancellationToken.None);

        Assert.Null(story);
    }

    private static HackerNewsHttpClientAdapter CreateAdapter(
        StubHttpMessageHandler handler,
        HackerNewsOptions? options = null,
        string baseAddress = "https://hacker-news.test/v0/") =>
        new(new StubHttpClientFactory(handler, baseAddress), HackerNewsResiliencePipeline.Create(options ?? PermissiveOptions()));

    private static HackerNewsOptions PermissiveOptions() => new()
    {
        RetryCount = 0,
        RetryBaseDelayMilliseconds = 1,
        CircuitBreakerFailures = 1000,
        CircuitBreakerSeconds = 30,
        TimeoutSeconds = 30,
    };
}
