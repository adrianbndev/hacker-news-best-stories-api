using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackerNews.Application.Ports.Outbound;
using HackerNews.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNews.Api.IntegrationTests;

public class SnapshotConcurrencyTests
{
    [Fact]
    public async Task Serves_parallel_requests_without_calling_hacker_news_again()
    {
        using var factory = new StoriesApiFactory();
        factory.HackerNewsClient.BestStoryIds = [1, 2, 3, 4, 5];
        factory.HackerNewsClient.ItemFactory = id => new Story(
            $"Story {id}",
            $"https://example.test/{id}",
            "author",
            DateTimeOffset.UnixEpoch,
            id * 100,
            0);

        using var client = factory.CreateClient();
        await WaitUntilSnapshotIsReady(factory);

        factory.HackerNewsClient.ResetCounters();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 50).Select(_ => client.GetAsync("/api/stories/best?n=5")));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(0, factory.HackerNewsClient.TotalCalls);

        var payload = await responses[0].Content.ReadFromJsonAsync<JsonElement>();
        var scores = payload.EnumerateArray().Select(story => story.GetProperty("score").GetInt32()).ToList();
        Assert.Equal(new[] { 500, 400, 300, 200, 100 }, scores);
    }

    private static async Task WaitUntilSnapshotIsReady(StoriesApiFactory factory)
    {
        var store = factory.Services.GetRequiredService<IStoryStore>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);

        while (store.GetSnapshot().Count == 0)
        {
            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException("The best stories snapshot was not populated within 10 seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20));
        }
    }
}
