using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using HackerNews.BestStories.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HackerNews.BestStories.UnitTests;

public class GetBestStoriesQueryTests
{
    private readonly Mock<IHackerNewsClient> _newsClient = new();

    private static GetBestStoriesQuery BuildQuery(Mock<IHackerNewsClient> newsClient) =>
        new(newsClient.Object, NullLogger<GetBestStoriesQuery>.Instance);

    private static HackerNewsItem BuildItem(int id) =>
        new(id, $"Title {id}", $"https://{id}.com", $"user{id}", 1700000000 + id, 50 - id, id * 2);

    [Fact]
    public async Task ExecuteAsync_ValidCount_ReturnsRequestedStoriesOrderedByScoreDescending_WithAllFieldsMapped()
    {
        _newsClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 3 });
        _newsClient.Setup(c => c.GetStoryDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => BuildItem(id));

        var query = BuildQuery(_newsClient);

        var result = (await query.ExecuteAsync(3)).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 49, 48, 47 }, result.Select(s => s.Score).ToArray());
        Assert.Equal(new[] { "Title 1", "Title 2", "Title 3" }, result.Select(s => s.Title).ToArray());
        Assert.Equal("user1", result[0].PostedBy);
        Assert.Equal("https://1.com", result[0].Uri);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000001), result[0].Time);
        Assert.Equal(2, result[0].CommentCount);
    }

    [Fact]
    public async Task ExecuteAsync_SomeDetailsAreNull_FiltersNullsAndReturnsOrderedValidStoriesWithoutThrowing()
    {
        _newsClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 3, 4 });
        _newsClient.Setup(c => c.GetStoryDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => id % 2 == 0 ? null : BuildItem(id));

        var query = BuildQuery(_newsClient);

        var result = (await query.ExecuteAsync(4)).ToList();

        Assert.Equal(new[] { 49, 47 }, result.Select(s => s.Score).ToArray());
        Assert.Equal(new[] { "Title 1", "Title 3" }, result.Select(s => s.Title).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_ItemMissingTitleOrBy_IsFilteredOut_AndMissingUrl_FallsBackToItemPage()
    {
        _newsClient.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 3, 4 });
        _newsClient.Setup(c => c.GetStoryDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) =>
            {
                return id switch
                {
                    2 => new HackerNewsItem(2, "", "https://2.com", "user2", 1700000002, 48, 4),
                    3 => new HackerNewsItem(3, "Title 3", "https://3.com", null!, 1700000003, 47, 6),
                    4 => new HackerNewsItem(4, "Title 4", null!, "user4", 1700000004, 46, 8),
                    _ => BuildItem(1)
                };
            });

        var query = BuildQuery(_newsClient);

        var result = (await query.ExecuteAsync(4)).ToList();

        Assert.Equal(new[] { 49, 46 }, result.Select(s => s.Score).ToArray());
        Assert.Equal("https://news.ycombinator.com/item?id=4", result.Single(s => s.Score == 46).Uri);
    }
}