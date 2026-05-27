using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IGroupAiService
{
    Task<Result<GroupAiActionItemsResponseDto>> ExtractActionItemsAsync(
        Guid groupId,
        GroupAiActionItemsRequest request,
        CancellationToken ct = default);
}
