using System.Text.Json.Serialization;

namespace HackerNews.BestStories.Application.DTOs
{
    /// <summary>
    /// Represent the structure of an "Item" as returned by the official Hacker News API.
    /// Use atributes from System.Text.Json to map the exact names from the external API without contaminating C# naming conventions (PascalCase). 
    /// Also, we map descendants which is where Hacker News stores the comment count.
    /// </summary>
    public record HackerNewsItem(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("by")] string By,
        [property: JsonPropertyName("time")] long Time, // Unix epoch time
        [property: JsonPropertyName("score")] int Score,
        [property: JsonPropertyName("descendants")] int Descendants // Equivale al commentCount
    );
}
