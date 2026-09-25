using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using HackerNews.BestStories.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HackerNews.BestStories.UnitTests;

public class HackerNewsCacheDecoratorTests
{
    private readonly Mock<IHackerNewsClient> _inner = new();
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    private HackerNewsCacheDecorator CreateSut() =>
        new(_inner.Object, _memoryCache, NullLogger<HackerNewsCacheDecorator>.Instance);

    [Fact]
    public async Task GetBestStoryIdsAsync_CalledTwice_InnerClientInvokedOnlyOnce()
    {
        _inner.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 3 });

        var sut = CreateSut();

        var firstCall = await sut.GetBestStoryIdsAsync();
        var secondCall = await sut.GetBestStoryIdsAsync();

        Assert.Equal(new[] { 1, 2, 3 }, firstCall);
        Assert.Equal(new[] { 1, 2, 3 }, secondCall);
        _inner.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStoryDetailsAsync_CalledTwice_InnerClientInvokedOncePerStory()
    {
        _inner.Setup(c => c.GetStoryDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, CancellationToken _) => new HackerNewsItem(id, $"Title {id}", $"https://{id}.com", "user", id, id, 0));

        var sut = CreateSut();

        var firstCall = await sut.GetStoryDetailsAsync(42);
        var secondCall = await sut.GetStoryDetailsAsync(42);

        Assert.Equal(42, firstCall!.Id);
        Assert.Equal(42, secondCall!.Id);
        _inner.Verify(c => c.GetStoryDetailsAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetStoryDetailsAsync_InnerReturnsNull_ReturnsNullWithoutThrowing()
    {
        _inner.Setup(c => c.GetStoryDetailsAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((HackerNewsItem?)null);

        var sut = CreateSut();

        var result = await sut.GetStoryDetailsAsync(42);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBestStoryIdsAsync_ConcurrentColdCacheCalls_InnerClientInvokedOnlyOnce()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _inner.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async _ =>
            {
                await gate.Task;
                return new[] { 1, 2, 3 };
            });

        var sut = CreateSut();

        var first = sut.GetBestStoryIdsAsync();
        var second = sut.GetBestStoryIdsAsync();

        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Equal(new[] { 1, 2, 3 }, results[0]);
        Assert.Equal(new[] { 1, 2, 3 }, results[1]);
        _inner.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBestStoryIdsAsync_OneWaiterCancels_SharedFetchCompletesForOtherWaiters()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _inner.Setup(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async _ =>
            {
                await gate.Task;
                return new[] { 1, 2, 3 };
            });

        var sut = CreateSut();

        using var canceledCts = new CancellationTokenSource();
        var canceledCaller = sut.GetBestStoryIdsAsync(canceledCts.Token);

        canceledCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledCaller);

        gate.SetResult();
        var otherCaller = await sut.GetBestStoryIdsAsync();

        Assert.Equal(new[] { 1, 2, 3 }, otherCaller);
        _inner.Verify(c => c.GetBestStoryIdsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}