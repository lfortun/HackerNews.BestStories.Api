using HackerNews.BestStories.Application.DTOs;

namespace HackerNews.BestStories.Application.Interfaces
{
    /// <summary>
    /// Define the contract for communication with the external Hacker News service.
    /// </summary>
    public interface IHackerNewsClient
    {
        /// <summary>
        /// Get the complete list of IDs for the current best stories.
        /// </summary>
        Task<IEnumerable<int>> GetBestStoryIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the details of a specific story by its ID.
        /// </summary>
        Task<HackerNewsItem?> GetStoryDetailsAsync(int storyId, CancellationToken cancellationToken = default);
    }
}
