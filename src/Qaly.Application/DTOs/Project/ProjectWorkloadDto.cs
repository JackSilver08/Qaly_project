namespace Qaly.Application.DTOs.Project;

public record ProjectWorkloadDto(
    Guid ProjectId,
    IEnumerable<MemberWorkloadDto> MembersWorkload);

public record MemberWorkloadDto(
    Guid UserId,
    string UserName,
    string? AvatarUrl,
    int TaskCount,
    int EstimatedHours,
    int ActualHours,
    int CompletedTaskCount);
