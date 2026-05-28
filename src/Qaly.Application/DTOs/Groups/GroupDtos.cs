using Qaly.Application.DTOs.Project;

namespace Qaly.Application.DTOs.Groups;

public record GroupDto(
    Guid Id,
    string Name,
    string? Description,
    string? AvatarUrl,
    string? Color,
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
    string? Description,
    Guid? OrganizationId = null,
    string? AvatarUrl = null,
    string? Color = null);

public record UpdateGroupRequest(
    string Name,
    string? Description,
    string? AvatarUrl,
    string? Color,
    string Status = "Active");

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
    DateTimeOffset? EditedAt);

public record SendGroupMessageRequest(
    string Content,
    string MessageType = "Text");

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
