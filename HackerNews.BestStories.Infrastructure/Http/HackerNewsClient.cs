using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace HackerNews.BestStories.Infrastructure.Http
{
    /// <summary>
    /// Implementation based on HttpClient to consume the Hacker News API.
    /// </summary>
    public class HackerNewsClient : IHackerNewsClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<HackerNewsClient> _logger;

        public HackerNewsClient(HttpClient httpClient, ILogger<HackerNewsClient> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Requesting best story IDs from Hacker News API.");

                var ids = await _httpClient.GetFromJsonAsync<IEnumerable<int>>(
                    "beststories.json",
                    cancellationToken);

                return ids ?? Enumerable.Empty<int>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching best story IDs.");
                throw;
            }
        }

        public async Task<HackerNewsItem?> GetStoryDetailsAsync(int storyId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting details for story ID: {StoryId}.", storyId);

                return await _httpClient.GetFromJsonAsync<HackerNewsItem>(
                    $"item/{storyId}.json",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while fetching details for story ID {StoryId}.", storyId);

                // Return null to handle it gracefully in the upper layer without breaking the parallel flow
                return null; 
            }
        }
    }
}