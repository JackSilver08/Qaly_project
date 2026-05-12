namespace Qaly.Application.DTOs.Comment;

public record CommentDto(
    Guid Id,
    string Content,
    Guid TaskItemId,
    Guid AuthorId,
    string AuthorName,
    string? AuthorAvatarUrl,
    Guid? ParentCommentId,
    int UpvoteCount,
    int DownvoteCount,
    int AttachmentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record CreateCommentDto(
    string Content,
    Guid TaskItemId,
    Guid? ParentCommentId = null,
    IReadOnlyList<Guid>? MentionedUserIds = null);
