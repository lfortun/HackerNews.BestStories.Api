using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;

namespace HackerNews.BestStories.Application.Services
{
    public class GetBestStoriesQuery : IGetBestStoriesQuery
    {
        private readonly IHackerNewsClient _newsClient;
        private const int MaxConcurrentRequests = 10; // Limit to avoid saturating network sockets

        public GetBestStoriesQuery(IHackerNewsClient newsClient)
        {
            _newsClient = newsClient ?? throw new ArgumentNullException(nameof(newsClient));
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

            //TODO: La "degradación graciosa" (en inglés, Graceful Degradation) es un principio de diseño de software que significa que, si una parte del sistema falla, la aplicación no se destruye por completo ni le muestra una pantalla de error genérica al usuario, sino que sigue funcionando con capacidades reducidas.
            // 3. Filter nulls (graceful degradation), order by Score descending, and map to the final DTO
            return itemsResult
                .Where(item => item != null)
                .Select(item => MapToResponse(item!))
                .OrderByDescending(story => story.Score);
        }

        /// <summary>
        /// Map the external entity to our output contract in a clean way.
        /// Translates the Unix epoch (seconds) to a DateTimeOffset in ISO 8601 UTC format.
        /// </summary>
        private static StoryResponse MapToResponse(HackerNewsItem item)
        {
            return new StoryResponse(
                Title: item.Title,
                Uri: item.Url,
                PostedBy: item.By,
                Time: DateTimeOffset.FromUnixTimeSeconds(item.Time).ToUniversalTime(),
                Score: item.Score,
                CommentCount: item.Descendants
            );
        }
    }
}
