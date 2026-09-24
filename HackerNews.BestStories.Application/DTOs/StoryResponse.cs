namespace HackerNews.BestStories.Application.DTOs
{
    /// <summary>
    /// Use record because it provides intrinsic immutability and value equality, ideal for DTOs.
    /// Response object that represents the strict format required by the client.
    /// Use DateTimeOffset instead of DateTime to ensure correct handling of time zones (ISO 8601 format with offsets like +00:00).
    /// </summary>
    public record StoryResponse(
        string Title,
        string Uri,
        string PostedBy,
        DateTimeOffset Time,
        int Score,
        int CommentCount
    );
}
