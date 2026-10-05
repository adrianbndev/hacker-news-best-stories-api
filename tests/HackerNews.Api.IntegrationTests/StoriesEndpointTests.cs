using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.Api.IntegrationTests;

public class StoriesEndpointTests
{
    private static readonly Story ExampleStory = new(
        "A uBlock Origin update was rejected from the Chrome Web Store",
        "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
        "ismaildonmez",
        DateTimeOffset.FromUnixTimeSeconds(1570887781),
        1716,
        572);

    [Fact]
    public async Task Returns_the_requested_stories_ordered_by_score()
    {
        using var factory = CreateFactory([ExampleStory, SmallStory("mid", 200), SmallStory("low", 10)]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best?n=2");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var scores = payload.EnumerateArray().Select(story => story.GetProperty("score").GetInt32());
        Assert.Equal(new[] { 1716, 200 }, scores);
    }

    [Fact]
    public async Task Reproduces_the_example_payload_shape()
    {
        using var factory = CreateFactory([ExampleStory]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best?n=10");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var story = payload.EnumerateArray().Single();
        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", story.GetProperty("title").GetString());
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", story.GetProperty("uri").GetString());
        Assert.Equal("ismaildonmez", story.GetProperty("postedBy").GetString());
        Assert.Equal("2019-10-12T13:43:01+00:00", story.GetProperty("time").GetString());
        Assert.Equal(1716, story.GetProperty("score").GetInt32());
        Assert.Equal(572, story.GetProperty("commentCount").GetInt32());
    }

    [Fact]
    public async Task Uses_a_default_of_ten_when_n_is_omitted()
    {
        var stories = Enumerable.Range(1, 15).Select(index => SmallStory($"story-{index}", index)).ToList();
        using var factory = CreateFactory(stories);
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>("/api/stories/best");

        Assert.Equal(10, payload.GetArrayLength());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(500, 15)]
    public async Task Accepts_the_boundary_counts(int n, int expectedCount)
    {
        var stories = Enumerable.Range(1, 15).Select(index => SmallStory($"story-{index}", index)).ToList();
        using var factory = CreateFactory(stories);
        using var client = factory.CreateClient();

        var payload = await client.GetFromJsonAsync<JsonElement>($"/api/stories/best?n={n}");

        Assert.Equal(expectedCount, payload.GetArrayLength());
    }

    [Theory]
    [InlineData(501)]
    [InlineData(1000)]
    [InlineData(2147483647)]
    public async Task Returns_everything_available_for_a_large_n(int n)
    {
        var stories = Enumerable.Range(1, 15).Select(index => SmallStory($"story-{index}", index)).ToList();
        using var factory = CreateFactory(stories);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/stories/best?n={n}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(15, payload.GetArrayLength());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("5000000000")]
    public async Task Rejects_an_invalid_n_with_a_unified_problem_details(string n)
    {
        using var factory = CreateFactory([ExampleStory]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/stories/best?n={n}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal("Invalid story count", problem.GetProperty("title").GetString());

        var detail = problem.GetProperty("detail").GetString();
        Assert.NotNull(detail);
        Assert.DoesNotContain("Parameter", detail);
    }

    [Fact]
    public async Task Returns_503_with_retry_after_when_the_snapshot_is_not_ready()
    {
        using var factory = CreateFactory([]);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("5", response.Headers.GetValues("Retry-After").Single());
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(503, problem.GetProperty("status").GetInt32());
        Assert.Equal("Best stories are not ready yet", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Serves_the_first_request_after_warm_up_without_503()
    {
        using var factory = new StoriesApiFactory { DisableBackgroundRefresh = true };
        factory.HackerNewsClient.BestStoryIds = [1, 2];
        factory.HackerNewsClient.ItemFactory = id => SmallStory($"story-{id}", id);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/stories/best?n=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, payload.GetArrayLength());
    }

    private static StoriesApiFactory CreateFactory(IReadOnlyList<Story> stories)
    {
        var factory = new StoriesApiFactory { DisableBackgroundRefresh = true };
        factory.Services.GetRequiredService<IStoryStore>().Swap(stories);
        return factory;
    }

    private static Story SmallStory(string title, int score) =>
        new(title, $"https://example.test/{title}", "author", DateTimeOffset.UnixEpoch, score, 0);
}
