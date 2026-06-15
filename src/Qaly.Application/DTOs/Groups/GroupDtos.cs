using Qaly.Application.DTOs.Project;

namespace Qaly.Application.DTOs.Groups;

public record GroupDto(
    Guid Id,
    string Name,
    string? AvatarUrl,
    string? Color,
    string? BackgroundTheme,
    string? BackgroundImageUrl,
    string Status,
    Guid OwnerId,
    string OwnerName,
    Guid? OrganizationId,
    string? OrganizationName,
    string CurrentUserRole,
    int MemberCount,
    int MessageCount,
    int OpenPollCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public record CreateGroupRequest(
    string Name,
    Guid? OrganizationId = null,
    string? AvatarUrl = null,
    string? Color = null);

public record UpdateGroupRequest(
    string Name,
    string? AvatarUrl,
    string? Color,
    string Status = "Active");

public record UpdateGroupBackgroundRequest(
    string? Theme,
    string? ImageUrl);

public record GroupMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);

public record AddGroupMemberRequest(
    Guid UserId,
    string Role = "Member");

public record CreateGroupInvitationRequest(
    string Email);

public record GroupInvitationDto(
    Guid Id,
    Guid GroupId,
    string Email,
    string Status,
    DateTimeOffset ExpiredAt,
    DateTimeOffset CreatedAt);

public record CreateGroupPollOptionRequest(
    string Content);

public record CreateGroupPollRequest(
    string Question,
    IReadOnlyList<CreateGroupPollOptionRequest> Options,
    bool AllowMultiple,
    DateTimeOffset? ExpiredAt = null);

public record UpdateGroupPollRequest(
    string Question,
    IReadOnlyList<CreateGroupPollOptionRequest> Options,
    bool AllowMultiple,
    DateTimeOffset? ExpiredAt = null);

public record GroupPollOptionDto(
    Guid Id,
    string Content,
    int SortOrder);

public record GroupPollDto(
    Guid Id,
    Guid GroupId,
    string Question,
    bool AllowMultiple,
    string Status,
    DateTimeOffset? ExpiredAt,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<GroupPollOptionDto> Options);

public record VoteGroupPollRequest(
    IReadOnlyList<Guid> OptionIds);

public record GroupPollOptionResultDto(
    Guid OptionId,
    string Content,
    int SortOrder,
    int VoteCount,
    IReadOnlyList<GroupPollVoterDto> Voters);

public record GroupPollVoterDto(
    Guid UserId,
    string FullName,
    string Email);

public record GroupPollResultsDto(
    Guid PollId,
    Guid GroupId,
    string Question,
    bool AllowMultiple,
    string Status,
    DateTimeOffset? ExpiredAt,
    int TotalVotes,
    int TotalVoters,
    IReadOnlyList<GroupPollOptionResultDto> Options,
    IReadOnlyList<Guid> CurrentUserOptionIds);

public record UpdateGroupMemberRoleRequest(
    string Role);

public record GroupMessageDto(
    Guid Id,
    Guid WorkGroupId,
    Guid UserId,
    string SenderName,
    string? SenderAvatarUrl,
    string Content,
    string MessageType,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt,
    bool IsPinned,
    DateTimeOffset? PinnedAt,
    Guid? PinnedByUserId,
    IReadOnlyList<GroupMessageReactionDto> Reactions);

public record SendGroupMessageRequest(
    string Content,
    string MessageType = "Text");

public record UpdateGroupMessageRequest(
    string Content);

public record SetGroupMessagePinRequest(
    bool IsPinned);

public record ReactToGroupMessageRequest(
    string Emoji);

public record GroupMessageReactionDto(
    string Emoji,
    int Count,
    IReadOnlyList<Guid> UserIds,
    bool ReactedByCurrentUser);

public record GroupAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long FileSize,
    DateTimeOffset CreatedAt);

public record GroupAttachmentDownloadDto(
    Stream Stream,
    string FileName,
    string ContentType);

public record CreateProjectFromGroupRequest(
    string Name,
    string? Code,
    string? Description,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);

public record CreateProjectFromGroupResult(
    ProjectDto Project,
    int MembersAdded,
    IReadOnlyList<Guid> AddedUserIds,
    IReadOnlyList<string> Warnings);
