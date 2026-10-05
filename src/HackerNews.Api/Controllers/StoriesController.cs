using HackerNews.Application;
using HackerNews.Application.Ports.Inbound;
using HackerNews.Domain;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.Api.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController : ControllerBase
{
    private readonly IGetBestStoriesUseCase _getBestStories;

    public StoriesController(IGetBestStoriesUseCase getBestStories) => _getBestStories = getBestStories;

    [HttpGet("best")]
    [ProducesResponseType(typeof(IReadOnlyList<Story>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBest([FromQuery] int n = 10, CancellationToken ct = default)
    {
        try
        {
            var stories = await _getBestStories.ExecuteAsync(n, ct);
            return Ok(stories);
        }
        catch (InvalidStoryCountException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid story count",
                detail: exception.Message);
        }
        catch (BestStoriesNotReadyException exception)
        {
            Response.Headers["Retry-After"] = "5";
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Best stories are not ready yet",
                detail: exception.Message);
        }
    }
}
