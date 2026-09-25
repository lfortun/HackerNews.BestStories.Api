using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HackerNews.BestStories.Application.Services
{
    public class GetBestStoriesQuery : IGetBestStoriesQuery
    {
        private readonly IHackerNewsClient _newsClient;
        private readonly ILogger<GetBestStoriesQuery> _logger;
        private const int MaxConcurrentRequests = 10; // Limit to avoid saturating network sockets

        public GetBestStoriesQuery(IHackerNewsClient newsClient, ILogger<GetBestStoriesQuery> logger)
        {
            _newsClient = newsClient ?? throw new ArgumentNullException(nameof(newsClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<StoryResponse>> ExecuteAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return Enumerable.Empty<StoryResponse>();
            }

            // 1. Get the complete list of best story IDs from Hacker News
            var allIds = await _newsClient.GetBestStoryIdsAsync(cancellationToken);

            // Take only the 'n' number of requested stories
            var targetIds = allIds.Take(count).ToList();

            // 2. Control concurrency using SemaphoreSlim to limit the number of concurrent requests
            using var semaphore = new SemaphoreSlim(MaxConcurrentRequests);

            var tasks = targetIds.Select(async id =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    // Get asynchronously and independently each story
                    return await _newsClient.GetStoryDetailsAsync(id, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Executing them in parallel but respecting the concurrency limit
            var itemsResult = await Task.WhenAll(tasks);

            // 3. Filter nulls and invalid items (graceful degradation: a missing/dead/deleted story must not fail the whole request), order by Score descending, and map to the final DTO
            var validItems = itemsResult
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.By));

            var filteredCount = itemsResult.Length - validItems.Count();
            if (filteredCount > 0)
            {
                _logger.LogWarning("Graceful degradation: {RequestedCount} story details were requested, {FilteredCount} of them were discarded (missing, deleted or invalid).", targetIds.Count, filteredCount);
            }

            return validItems
                .Select(item => MapToResponse(item!))
                .OrderByDescending(story => story.Score);
        }

        /// <summary>
        /// Map the external entity to our output contract in a clean way.
        /// Translates the Unix epoch (seconds) to a DateTimeOffset in ISO 8601 UTC format.
        /// Stories without a url (Ask/Show HN) fall back to the public Hacker News item page.
        /// </summary>
        private static StoryResponse MapToResponse(HackerNewsItem item)
        {
            var storyUri = string.IsNullOrWhiteSpace(item.Url) ? $"https://news.ycombinator.com/item?id={item.Id}" : item.Url;

            return new StoryResponse(
                Title: item.Title,
                Uri: storyUri,
                PostedBy: item.By,
                Time: DateTimeOffset.FromUnixTimeSeconds(item.Time),
                Score: item.Score,
                CommentCount: item.Descendants
            );
        }
    }
}
