namespace HackerNews.BestStories.Application.Options
{
    // <summary>
    /// Map the "HackerNewsApi" section of the appsettings.json to a strongly typed class.
    /// </summary>
    public class HackerNewsOptions
    {
        public const string SectionName = "HackerNewsApi";

        public string BaseUrl { get; set; } = string.Empty;

        public int Timeout { get; set; } = 5; // Default timeout in seconds
    }
}
