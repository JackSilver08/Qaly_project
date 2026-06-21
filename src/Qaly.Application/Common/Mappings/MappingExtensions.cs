using Qaly.Application.DTOs.Attachment;
using Qaly.Application.DTOs.Comment;
using Qaly.Application.DTOs.Project;
using Qaly.Application.DTOs.Task;
using Qaly.Application.DTOs.User;
using Qaly.Domain.Entities;

namespace Qaly.Application.Common.Mappings;

public static class MappingExtensions
{
    public static UserDto ToDto(this User user)
        => new(
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            user.IsActive,
            user.AvatarUrl,
            user.CreatedAt);

    public static ProjectDto ToDto(this Project project)
        => new(
            project.Id,
            project.Name,
            project.Code,
            project.Description,
            project.LogoUrl,
            project.Status,
            project.StartDate,
            project.EndDate,
            project.OwnerId,
            project.Owner?.FullName ?? string.Empty,
            project.Members?.Count ?? 0,
            project.Tasks?.Count ?? 0,
            CalculateProgress(project.Tasks),
            project.Labels?.Select(label => label.ToDto()).ToList() ?? [],
            project.CreatedAt,
            project.OrganizationId,
            project.Organization?.Name,
            project.SourceGroupId,
            project.EnableOnHold,
            project.EnableInReview,
            project.RequireEvidenceToDone,
            project.RestrictTransitionsToAdmin,
            project.DeletedAt);

    public static Project ToEntity(this CreateProjectDto dto)
        => new()
        {
            Name = dto.Name,
            Code = dto.Code ?? string.Empty,
            Description = dto.Description,
            LogoUrl = dto.LogoUrl,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            OrganizationId = dto.OrganizationId,
            SourceGroupId = dto.SourceGroupId
        };

    public static void ApplyTo(this UpdateProjectDto dto, Project project)
    {
        project.Name = dto.Name;
        project.Code = dto.Code ?? project.Code;
        project.Description = dto.Description;
        project.LogoUrl = dto.LogoUrl;
        project.Status = dto.Status;
        project.StartDate = dto.StartDate;
        project.EndDate = dto.EndDate;

        if (dto.EnableOnHold.HasValue) project.EnableOnHold = dto.EnableOnHold.Value;
        if (dto.EnableInReview.HasValue) project.EnableInReview = dto.EnableInReview.Value;
        if (dto.RequireEvidenceToDone.HasValue) project.RequireEvidenceToDone = dto.RequireEvidenceToDone.Value;
        if (dto.RestrictTransitionsToAdmin.HasValue) project.RestrictTransitionsToAdmin = dto.RestrictTransitionsToAdmin.Value;
    }

    public static OrganizationDto ToDto(this Organization organization)
        => new(
            organization.Id,
            organization.Name,
            organization.Code,
            organization.Description,
            organization.IsActive,
            organization.OwnerId,
            organization.Owner?.FullName ?? string.Empty,
            organization.Members?.Count ?? 0,
            organization.Projects?.Count ?? 0,
            organization.CreatedAt,
            organization.AllowedEmailDomains,
            organization.WorkspaceIcon,
            organization.WorkspaceCover);

    public static ProjectLabelDto ToDto(this ProjectLabel label)
        => new(label.Id, label.Name, label.Color, label.CreatedAt);

    public static SprintDto ToDto(this Sprint sprint)
    {
        var taskCount = sprint.Tasks?.Count ?? 0;
        var completedCount = sprint.Tasks?.Count(t => string.Equals(t.Status, "Done", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var progress = taskCount > 0 ? (int)Math.Round((double)completedCount / taskCount * 100) : 0;

        return new SprintDto(
            sprint.Id,
            sprint.ProjectId,
            sprint.Name,
            sprint.StartDate,
            sprint.EndDate,
            sprint.Status,
            sprint.Goal,
            taskCount,
            completedCount,
            progress);
    }

    public static Sprint ToEntity(this CreateSprintRequest dto, Guid projectId)
        => new()
        {
            ProjectId = projectId,
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Goal = dto.Goal,
            Status = "Planning"
        };

    public static void ApplyTo(this UpdateSprintRequest dto, Sprint sprint)
    {
        sprint.Name = dto.Name;
        sprint.StartDate = dto.StartDate;
        sprint.EndDate = dto.EndDate;
        sprint.Status = dto.Status;
        sprint.Goal = dto.Goal;
    }

    public static TaskItemDto ToDto(this TaskItem task, bool isRestricted = false)
    {
        var restrictedTitle = $"Restricted Task #{task.Id.ToString()[..8]}";
        var taskTitle = isRestricted ? restrictedTitle : task.Title;
        var description = isRestricted ? null : task.Description;
        var commentCount = isRestricted ? 0 : task.Comments?.Count ?? 0;
        var attachmentCount = isRestricted ? 0 : task.Attachments?.Count ?? 0;
        List<TaskAssigneeDto> assignees = isRestricted
            ? []
            : task.Assignees?.Select(assignment => new TaskAssigneeDto(
                assignment.UserId,
                assignment.User?.FullName ?? string.Empty,
                assignment.User?.AvatarUrl)).ToList() ?? [];
        List<TaskLabelDto> labels = isRestricted
            ? []
            : task.Labels?.Select(label => new TaskLabelDto(
                label.ProjectLabelId,
                label.ProjectLabel?.Name ?? string.Empty,
                label.ProjectLabel?.Color ?? "#64748B")).ToList() ?? [];

        return new(
            task.Id,
            taskTitle,
            description,
            task.Status,
            task.Priority,
            task.DueDate,
            task.EstimatedHours,
            task.ActualHours,
            task.IsPrivate,
            isRestricted,
            task.IsPinned,
            task.ContributesToProgress,
            task.UpvoteCount,
            task.DownvoteCount,
            task.ProjectId,
            task.Project?.Name ?? string.Empty,
            isRestricted ? null : task.AssigneeId,
            isRestricted ? null : task.Assignee?.FullName,
            assignees,
            labels,
            task.ReporterId,
            isRestricted ? string.Empty : task.Reporter?.FullName ?? string.Empty,
            commentCount,
            attachmentCount,
            null,
            task.CreatedAt,
            task.SortOrder,
            EncodeRowVersion(task.RowVersion));
    }

    public static TaskItemDto ToDto(this TaskItem task)
        => task.ToDto(false);

    public static TaskItemDto ToRestrictedDto(this TaskItem task)
        => task.ToDto(true);

    private static int CalculateProgress(ICollection<TaskItem>? tasks)
    {
        if (tasks == null || tasks.Count == 0)
        {
            return 0;
        }

        var trackedTasks = tasks
            .Where(task => task.ContributesToProgress && !string.Equals(task.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (trackedTasks.Count == 0)
        {
            return 0;
        }

        return (int)Math.Round(
            trackedTasks.Count(task => string.Equals(task.Status, "Done", StringComparison.OrdinalIgnoreCase)) * 100d / trackedTasks.Count,
            MidpointRounding.AwayFromZero);
    }

    /*
     * Kept intentionally close to the old shape so callers that do not know about
     * role-based masking still get the full DTO by default.
     */
    private static TaskItemDto LegacyToDto(this TaskItem task)
        => new(
            task.Id,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.DueDate,
            task.EstimatedHours,
            task.ActualHours,
            task.IsPrivate,
            false,
            task.IsPinned,
            task.ContributesToProgress,
            task.UpvoteCount,
            task.DownvoteCount,
            task.ProjectId,
            task.Project?.Name ?? string.Empty,
            task.AssigneeId,
            task.Assignee?.FullName,
            task.Assignees?.Select(assignment => new TaskAssigneeDto(
                assignment.UserId,
                assignment.User?.FullName ?? string.Empty,
                assignment.User?.AvatarUrl)).ToList() ?? [],
            task.Labels?.Select(label => new TaskLabelDto(
                label.ProjectLabelId,
                label.ProjectLabel?.Name ?? string.Empty,
                label.ProjectLabel?.Color ?? "#64748B")).ToList() ?? [],
            task.ReporterId,
            task.Reporter?.FullName ?? string.Empty,
            task.Comments?.Count ?? 0,
            task.Attachments?.Count ?? 0,
            null,
            task.CreatedAt,
            task.SortOrder,
            EncodeRowVersion(task.RowVersion));

    public static TaskItem ToEntity(this CreateTaskDto dto)
        => new()
        {
            Title = dto.Title,
            Description = dto.Description,
            Priority = dto.Priority,
            DueDate = dto.DueDate,
            EstimatedHours = dto.EstimatedHours,
            ProjectId = dto.ProjectId,
            AssigneeId = dto.AssigneeId,
            IsPrivate = dto.IsPrivate,
            IsPinned = dto.IsPinned,
            ContributesToProgress = dto.ContributesToProgress
        };

    public static void ApplyTo(this UpdateTaskDto dto, TaskItem task)
    {
        task.Title = dto.Title;
        task.Description = dto.Description;
        task.Status = dto.Status;
        task.Priority = dto.Priority;
        task.DueDate = dto.DueDate;
        task.EstimatedHours = dto.EstimatedHours;
        task.ActualHours = dto.ActualHours;
        task.AssigneeId = dto.AssigneeId;
        task.IsPrivate = dto.IsPrivate;
        task.IsPinned = dto.IsPinned;
        task.ContributesToProgress = dto.ContributesToProgress;
    }

    public static string EncodeRowVersion(byte[]? rowVersion)
        => Convert.ToBase64String(rowVersion is { Length: > 0 } ? rowVersion : []);

    public static CommentDto ToDto(this TaskComment comment)
        => new(
            comment.Id,
            comment.Content,
            comment.TaskItemId,
            comment.AuthorId,
            comment.Author?.FullName ?? string.Empty,
            comment.Author?.AvatarUrl,
            comment.ParentCommentId,
            comment.UpvoteCount,
            comment.DownvoteCount,
            comment.Attachments?.Count ?? 0,
            comment.CreatedAt,
            comment.UpdatedAt);

    public static TaskAttachmentDto ToDto(this TaskAttachment attachment)
        => new(
            attachment.Id,
            attachment.FileName,
            attachment.FilePath,
            attachment.FileSize,
            attachment.ContentType,
            attachment.Scope,
            attachment.ProjectId,
            attachment.TaskItemId,
            attachment.CommentId,
            attachment.UploadedById,
            attachment.UploadedBy?.FullName ?? string.Empty,
            attachment.UploadedAt,
            attachment.IsEvidence,
            attachment.EvidenceApprovalStatus,
            attachment.EvidenceReviewedById,
            attachment.EvidenceReviewedBy?.FullName,
            attachment.EvidenceReviewedAt,
            attachment.EvidenceReviewNote,
            attachment.ContentHash);
}
