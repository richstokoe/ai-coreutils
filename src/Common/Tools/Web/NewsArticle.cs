namespace AiCoreUtils.Common.Tools;

public record NewsArticle(
    string Title,
    string Url,
    string? ImageUrl,
    string? Summary,
    string Source,
    string? SourceImageUrl,
    DateTime PublishedAt
);
