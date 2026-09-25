using System.Text.Json;
using HackerNews.BestStories.Application.DTOs;

namespace HackerNews.BestStories.UnitTests;

public class StoryResponseTests
{
    [Fact]
    public void Serialize_UsesExactWireContractPropertyNames()
    {
        var story = new StoryResponse(
            "A uBlock Origin update was rejected from the Chrome Web Store",
            "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
            "ismaildonmez",
            DateTimeOffset.Parse("2019-10-12T13:43:01+00:00"),
            1716,
            572);

        var json = JsonSerializer.Serialize(story);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(6, root.EnumerateObject().Count());
        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", root.GetProperty("title").GetString());
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", root.GetProperty("uri").GetString());
        Assert.Equal("ismaildonmez", root.GetProperty("postedBy").GetString());
        Assert.Equal("2019-10-12T13:43:01+00:00", root.GetProperty("time").GetString());
        Assert.Equal(1716, root.GetProperty("score").GetInt32());
        Assert.Equal(572, root.GetProperty("commentCount").GetInt32());
    }
}