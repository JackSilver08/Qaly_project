using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Comment;

namespace Qaly.Application.Services;

public interface ICommentService
{
    Task<Result<IReadOnlyList<CommentDto>>> GetByTaskAsync(Guid taskItemId, CancellationToken ct = default);
    Task<Result<CommentDto>> CreateAsync(CreateCommentDto dto, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
