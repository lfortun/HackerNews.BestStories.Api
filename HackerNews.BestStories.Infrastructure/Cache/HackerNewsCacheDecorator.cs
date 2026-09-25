using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using HackerNews.BestStories.Application.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HackerNews.BestStories.Infrastructure.Cache
{
    /// <summary>
    /// Decorator that adds caching capabilities to the Hacker News client, allowing for improved performance and reduced load on the external API,
    /// following the Open/Closed principle of SOLID.
    /// Single-flight: a shared Lazy<Task<T>> is stored in cache so concurrent cold-cache misses coalesce into a single upstream call.
    /// </summary>
    public class HackerNewsCacheDecorator : IHackerNewsClient
    {
        private readonly IHackerNewsClient _innerClient;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<HackerNewsCacheDecorator> _logger;
        private readonly TimeSpan _idsCacheDuration;
        private readonly TimeSpan _storyCacheDuration;

        // keys for caching
        private const string BestStoryIdsCacheKey = "HN_BestStoryIds";
        private const string StoryDetailCacheKeyPrefix = "HN_Story_";

        public HackerNewsCacheDecorator(
            IHackerNewsClient innerClient,
            IMemoryCache memoryCache,
            ILogger<HackerNewsCacheDecorator> logger,
            IOptions<HackerNewsOptions> options)
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _idsCacheDuration = TimeSpan.FromSeconds(options.Value.BestStoryIdsCacheSeconds);
            _storyCacheDuration = TimeSpan.FromSeconds(options.Value.StoryDetailsCacheSeconds);
        }

        public async Task<IEnumerable<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default)
        {
            var lazy = _memoryCache.GetOrCreate(BestStoryIdsCacheKey, entry =>
            {
                _logger.LogInformation("Empty or expired cache for best story IDs. Calling external API.");
                entry.AbsoluteExpirationRelativeToNow = _idsCacheDuration;
                // Shared fetch: it does not react to the caller's token, so a single disconnect does not
                // abort the work for other waiters; Polly's per-attempt timeout still bounds it.
                return new Lazy<Task<IEnumerable<int>>>(() => _innerClient.GetBestStoryIdsAsync(CancellationToken.None));
            });

            try
            {
                return await lazy!.Value.WaitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed fetch must not be cached: the next request creates a new Lazy and retries.
                _memoryCache.Remove(BestStoryIdsCacheKey);
                throw;
            }
        }

        public async Task<HackerNewsItem?> GetStoryDetailsAsync(int storyId, CancellationToken cancellationToken = default)
        {
            string cacheKey = $"{StoryDetailCacheKeyPrefix}{storyId}";

            var lazy = _memoryCache.GetOrCreate(cacheKey, entry =>
            {
                _logger.LogDebug("Empty or expired cache for story ID: {StoryId}. Calling external API.", storyId);
                entry.AbsoluteExpirationRelativeToNow = _storyCacheDuration;
                return new Lazy<Task<HackerNewsItem?>>(() => _innerClient.GetStoryDetailsAsync(storyId, CancellationToken.None));
            });

            try
            {
                return await lazy!.Value.WaitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _memoryCache.Remove(cacheKey);
                throw;
            }
        }
    }
}