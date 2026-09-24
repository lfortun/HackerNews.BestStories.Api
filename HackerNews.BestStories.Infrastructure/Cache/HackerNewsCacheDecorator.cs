using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HackerNews.BestStories.Infrastructure.Cache
{
    /// <summary>
    /// Decorator that adds caching capabilities to the Hacker News client, allowing for improved performance and reduced load on the external API,
    /// following the Open/Closed principle of SOLID.
    /// </summary>
    public class HackerNewsCacheDecorator : IHackerNewsClient
    {
        private readonly IHackerNewsClient _innerClient;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<HackerNewsCacheDecorator> _logger;

        // keys for caching
        private const string BestStoryIdsCacheKey = "HN_BestStoryIds";
        private const string StoryDetailCacheKeyPrefix = "HN_Story_";

        // Times of expiration (TTL) for cache entries
        private static readonly TimeSpan IdsCacheDuration = TimeSpan.FromMinutes(1); // The best story rankings change quickly
        private static readonly TimeSpan StoryCacheDuration = TimeSpan.FromMinutes(15); // An old story rarely changes its base data

        public HackerNewsCacheDecorator(
            IHackerNewsClient innerClient,
            IMemoryCache memoryCache,
            ILogger<HackerNewsCacheDecorator> logger)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default)
        {
            return await _memoryCache.GetOrCreateAsync(BestStoryIdsCacheKey, async entry =>
            {
                _logger.LogInformation("Empty or expired cache for best story IDs. Calling external API.");
                entry.AbsoluteExpirationRelativeToNow = IdsCacheDuration;
                return await _innerClient.GetBestStoryIdsAsync(cancellationToken);
            }) ?? Enumerable.Empty<int>();
        }

        public async Task<HackerNewsItem?> GetStoryDetailsAsync(int storyId, CancellationToken cancellationToken = default)
        {
            string cacheKey = $"{StoryDetailCacheKeyPrefix}{storyId}";

            return await _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                _logger.LogDebug("Empty or expired cache for story ID: {StoryId}. Calling external API.", storyId);
                entry.AbsoluteExpirationRelativeToNow = StoryCacheDuration;
                return await _innerClient.GetStoryDetailsAsync(storyId, cancellationToken);
            });
        }
    }
}
