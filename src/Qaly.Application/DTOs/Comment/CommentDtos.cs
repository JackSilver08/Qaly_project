namespace Qaly.Application.DTOs.Comment;

public record CommentDto(
    Guid Id,
    string Content,
    Guid TaskItemId,
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record CreateCommentDto(
    string Content,
    Guid TaskItemId);
