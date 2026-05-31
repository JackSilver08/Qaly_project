using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Groups;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public interface IGroupsService
{
    Task<Result<PagedResult<GroupDto>>> GetMineAsync(int page = 1, int pageSize = 20, string? search = null, CancellationToken ct = default);
    Task<Result<GroupDto>> GetByIdAsync(Guid groupId, CancellationToken ct = default);
    Task<Result<GroupDto>> CreateAsync(CreateGroupRequest request, CancellationToken ct = default);
    Task<Result<GroupDto>> UpdateAsync(Guid groupId, UpdateGroupRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid groupId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GroupMemberDto>>> GetMembersAsync(Guid groupId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<GroupInvitationDto>>> GetInvitationsAsync(Guid groupId, string? status = null, CancellationToken ct = default);
    Task<Result<GroupInvitationDto>> CreateInvitationAsync(Guid groupId, CreateGroupInvitationRequest request, CancellationToken ct = default);
    Task<Result<GroupPollDto>> CreatePollAsync(Guid groupId, CreateGroupPollRequest request, CancellationToken ct = default);
    Task<Result<GroupPollResultsDto>> VotePollAsync(Guid groupId, Guid pollId, VoteGroupPollRequest request, CancellationToken ct = default);
    Task<Result<GroupPollDto>> ClosePollAsync(Guid groupId, Guid pollId, CancellationToken ct = default);
    Task<Result<GroupPollResultsDto>> GetPollResultsAsync(Guid groupId, Guid pollId, CancellationToken ct = default);
    Task<Result<GroupInvitationDto>> AcceptInvitationAsync(string token, CancellationToken ct = default);
    Task<Result<GroupInvitationDto>> RejectInvitationAsync(string token, CancellationToken ct = default);
    Task<Result> AddExistingMemberAsync(Guid groupId, AddGroupMemberRequest request, CancellationToken ct = default);
    Task<Result> UpdateMemberRoleAsync(Guid groupId, Guid userId, UpdateGroupMemberRoleRequest request, CancellationToken ct = default);
    Task<Result> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task<Result<PagedResult<GroupMessageDto>>> GetMessagesAsync(Guid groupId, int page = 1, int pageSize = 50, CancellationToken ct = default);
    Task<Result<GroupMessageDto>> CreateMessageAsync(Guid groupId, SendGroupMessageRequest request, CancellationToken ct = default);
    Task<Result<CreateProjectFromGroupResult>> CreateProjectFromGroupAsync(Guid groupId, CreateProjectFromGroupRequest request, CancellationToken ct = default);
    Task<Result<GroupMeetingSessionDto>> StartMeetingSessionAsync(Guid groupId, CancellationToken ct = default);
    Task<Result<GroupMeetingSessionDto>> JoinMeetingSessionAsync(Guid groupId, Guid meetingId, CancellationToken ct = default);
    Task<Result<GroupMeetingSessionDto>> EndMeetingSessionAsync(Guid groupId, Guid meetingId, CancellationToken ct = default);
    Task<bool> CanAccessGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<bool> CanManageGroupAsync(Guid groupId, CancellationToken ct = default);
}
