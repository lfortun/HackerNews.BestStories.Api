using HackerNews.BestStories.Application.DTOs;

namespace HackerNews.BestStories.Application.Interfaces
{
    /// <summary>
    /// Define the use case for retrieving the top N stories sorted by score in descending order.
    /// </summary>
    public interface IGetBestStoriesQuery
    {
        /// <summary>
        /// Execute the query to retrieve the top N stories from Hacker News.
        /// </summary>
        /// <param name="count">Number of stories to return (n).</param>
        Task<IEnumerable<StoryResponse>> ExecuteAsync(int count, CancellationToken cancellationToken = default);
    }
}
