using HackerNews.Api;
using HackerNews.Application.Ports.Inbound;
using HackerNews.Application.Ports.Outbound;
using HackerNews.Application.UseCases;
using HackerNews.Domain;
using HackerNews.Infrastructure.HackerNews;
using HackerNews.Infrastructure.Refresh;
using HackerNews.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new ProblemDetails
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid story count",
        Detail = $"n must be a positive integer (>= {BestStoriesLimits.MinimumCount}).",
    })
    {
        ContentTypes = { "application/problem+json" },
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHackerNewsOptions(builder.Configuration);

builder.Services.AddSingleton<IGetBestStoriesUseCase, GetBestStoriesUseCase>();
builder.Services.AddSingleton<IStoryStore, InMemoryStoryStoreAdapter>();
builder.Services.AddSingleton(provider =>
    HackerNewsResiliencePipeline.Create(provider.GetRequiredService<IOptions<HackerNewsOptions>>().Value));
builder.Services.AddHttpClient(HackerNewsHttpClientAdapter.HttpClientName, (provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<HackerNewsOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton<IHackerNewsClient, HackerNewsHttpClientAdapter>();
builder.Services.AddSingleton<BestStoriesRefresher>();
builder.Services.AddHostedService<BestStoriesRefreshService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

using (var warmUpTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
{
    var refresher = app.Services.GetRequiredService<BestStoriesRefresher>();
    try
    {
        await refresher.RefreshAsync(warmUpTimeout.Token);
    }
    catch (OperationCanceledException)
    {
        app.Logger.LogWarning("The initial best stories refresh timed out after 15 seconds; starting without a snapshot.");
    }
}

app.Run();

public partial class Program;

namespace HackerNews.Api
{
    public static class HackerNewsOptionsRegistration
    {
        public static IServiceCollection AddHackerNewsOptions(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<HackerNewsOptions>()
                .Bind(configuration.GetSection("HackerNews"))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<HackerNewsOptions>, HackerNewsOptionsValidator>();

            return services;
        }
    }
}
