using HackerNews.BestStories.Api.Controllers;
using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HackerNews.BestStories.UnitTests;

public class StoriesControllerTests
{
    private readonly Mock<IGetBestStoriesQuery> _query = new();
    private readonly StoriesController _controller;

    public StoriesControllerTests()
    {
        _controller = new StoriesController(_query.Object, NullLogger<StoriesController>.Instance);
    }

    [Fact]
    public async Task GetBestStories_ValidCount_ReturnsOkWithStoryList()
    {
        var expected = new StoryResponse[] { new("Title", "https://uri.com", "user", DateTimeOffset.UtcNow, 100, 5) };
        _query.Setup(q => q.ExecuteAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _controller.GetBestStories(5, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, okResult.Value);
        _query.Verify(q => q.ExecuteAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBestStories_NonPositiveCount_ReturnsBadRequest_AndDoesNotExecuteQuery()
    {
        var result = await _controller.GetBestStories(0, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequest.Value);
        _query.Verify(q => q.ExecuteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}