using System.Text.Json.Serialization;

namespace HackerNews.BestStories.Application.DTOs
{
    /// <summary>
    /// Use record because it provides intrinsic immutability and value equality, ideal for DTOs.
    /// Response object that represents the strict format required by the client.
    /// Use DateTimeOffset instead of DateTime to ensure correct handling of time zones (ISO 8601 format with offsets like +00:00).
    /// Explicit JsonPropertyName attributes pin the wire contract so it does not depend on serializer defaults.
    /// </summary>
    public record StoryResponse(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("uri")] string Uri,
        [property: JsonPropertyName("postedBy")] string PostedBy,
        [property: JsonPropertyName("time")] DateTimeOffset Time,
        [property: JsonPropertyName("score")] int Score,
        [property: JsonPropertyName("commentCount")] int CommentCount
    );
}
