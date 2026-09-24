using HackerNews.BestStories.Application.DTOs;
using HackerNews.BestStories.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HackerNews.BestStories.Api.Controllers
{
    /// <summary>
    /// API Controller responsible for handling requests related to Hacker News stories.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class StoriesController : ControllerBase
    {
        private readonly IGetBestStoriesQuery _getBestStoriesQuery;
        private readonly ILogger<StoriesController> _logger;

        public StoriesController(IGetBestStoriesQuery getBestStoriesQuery, ILogger<StoriesController> logger)
        {
            _getBestStoriesQuery = getBestStoriesQuery ?? throw new ArgumentNullException(nameof(getBestStoriesQuery));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Retrieves the details of the best 'n' stories from Hacker News API, ordered by score descending.
        /// Route Filters ({n:int}): By using route constraints in the [HttpGet("best/{n:int}")] attribute, 
        /// we prevent requests with non-numeric values (like api/stories/best/abc) from hitting the application code; 
        /// the framework automatically discards them.
        /// </summary>
        /// <param name="n">The number of best stories to retrieve. Must be greater than 0.</param>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        /// <returns>An array of the best stories.</returns>
        [HttpGet("best/{n:int}")]
        [ProducesResponseType(typeof(IEnumerable<StoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<StoryResponse>>> GetBestStories(int n, CancellationToken cancellationToken)
        {
            if (n <= 0)
            {
                _logger.LogWarning("Validation failed. Requested story count 'n' must be greater than zero. Provided value: {Count}", n);
                return BadRequest(new { Message = "The parameter 'n' must be a positive integer greater than zero." });
            }

            _logger.LogInformation("Processing request to fetch the best {Count} stories.", n);

            var stories = await _getBestStoriesQuery.ExecuteAsync(n, cancellationToken);

            return Ok(stories);
        }
    }
}
