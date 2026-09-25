namespace HackerNews.BestStories.Application.Options
{
    /// <summary>
    /// Map the "HackerNewsApi" section of the appsettings.json to a strongly typed class.
    /// </summary>
    public class HackerNewsOptions
    {
        public const string SectionName = "HackerNewsApi";

        public string BaseUrl { get; set; } = string.Empty;

        public int Timeout { get; set; } = 5; // Default timeout in seconds

        public int BestStoryIdsCacheSeconds { get; set; } = 60; // Best story rankings change quickly

        public int StoryDetailsCacheSeconds { get; set; } = 900; // An old story rarely changes its base data
    }
}
