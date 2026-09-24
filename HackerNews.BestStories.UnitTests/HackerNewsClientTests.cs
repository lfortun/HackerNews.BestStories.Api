using System.Net;
using System.Text;
using HackerNews.BestStories.Infrastructure.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerNews.BestStories.UnitTests;

public class HackerNewsClientTests
{
    private const string BaseUrl = "https://hacker-news.firebaseio.com/v0/";

    private static HackerNewsClient CreateSut(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var httpClient = new HttpClient(new FakeHttpMessageHandler(responder))
        {
            BaseAddress = new Uri(BaseUrl)
        };
        return new HackerNewsClient(httpClient, NullLogger<HackerNewsClient>.Instance);
    }

    [Fact]
    public async Task GetBestStoryIdsAsync_WithValidResponse_ReturnsDeserializedIds()
    {
        var sut = CreateSut(_ => JsonResponse("[1, 2, 3]"));

        var result = await sut.GetBestStoryIdsAsync();

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task GetStoryDetailsAsync_WithValidResponse_ReturnsMappedStoryDetails()
    {
        var sut = CreateSut(_ => JsonResponse(
            """{"id": 21233041, "title": "A uBlock Origin update was rejected from the Chrome Web Store", "url": "https://github.com/uBlockOrigin/uBlock-issues/issues/745", "by": "ismaildonmez", "time": 1570894981, "score": 1716, "descendants": 572}"""));

        var result = await sut.GetStoryDetailsAsync(21233041);

        Assert.NotNull(result);
        Assert.Equal(21233041, result!.Id);
        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", result.Title);
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", result.Url);
        Assert.Equal("ismaildonmez", result.By);
        Assert.Equal(1570894981, result.Time);
        Assert.Equal(1716, result.Score);
        Assert.Equal(572, result.Descendants);
    }

    [Fact]
    public async Task GetStoryDetailsAsync_ServerError_ReturnsNullWithoutThrowing()
    {
        var sut = CreateSut(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await sut.GetStoryDetailsAsync(21233041);

        Assert.Null(result);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}