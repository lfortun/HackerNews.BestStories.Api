using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using HackerNews.BestStories.Application.Services;
using Moq;

namespace HackerNews.BestStories.UnitTests;

public class GetBestStoriesQueryTests
{
    private readonly Mock<IHackerNewsClient> _newsClient = new();

    private static HackerNewsItem BuildItem(int id) =>
        new(id, $"Title {id}", $"https://{id}.com", $"user{id}", 1700000000 + id, 50 - id, id * 2);

    [Fact]
    public async Task ExecuteAsync_ValidCount_ReturnsRequestedStoriesOrderedByScoreDescending_WithAllFieldsMapped()
    {
        _newsClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 3 });
        _newsClient.Setup(c => c.GetStoryDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => BuildItem(id));

        var query = new GetBestStoriesQuery(_newsClient.Object);

        var result = (await query.ExecuteAsync(3)).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 49, 48, 47 }, result.Select(s => s.Score).ToArray());
        Assert.Equal(new[] { "Title 1", "Title 2", "Title 3" }, result.Select(s => s.Title).ToArray());
        Assert.Equal("user1", result[0].PostedBy);
        Assert.Equal("https://1.com", result[0].Uri);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000001).ToUniversalTime(), result[0].Time);
        Assert.Equal(2, result[0].CommentCount);
    }

    [Fact]
    public async Task ExecuteAsync_SomeDetailsAreNull_FiltersNullsAndReturnsOrderedValidStoriesWithoutThrowing()
    {
        _newsClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 3, 4 });
        _newsClient.Setup(c => c.GetStoryDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => id % 2 == 0 ? null : BuildItem(id));

        var query = new GetBestStoriesQuery(_newsClient.Object);

        var result = (await query.ExecuteAsync(4)).ToList();

        Assert.Equal(new[] { 49, 47 }, result.Select(s => s.Score).ToArray());
        Assert.Equal(new[] { "Title 1", "Title 3" }, result.Select(s => s.Title).ToArray());
    }
}