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
            project.Description,
            project.Status,
            project.StartDate,
            project.EndDate,
            project.OwnerId,
            project.Owner?.FullName ?? string.Empty,
            project.Members?.Count ?? 0,
            project.Tasks?.Count ?? 0,
            project.CreatedAt);

    public static Project ToEntity(this CreateProjectDto dto)
        => new()
        {
            Name = dto.Name,
            Description = dto.Description,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate
        };

    public static void ApplyTo(this UpdateProjectDto dto, Project project)
    {
        project.Name = dto.Name;
        project.Description = dto.Description;
        project.Status = dto.Status;
        project.StartDate = dto.StartDate;
        project.EndDate = dto.EndDate;
    }

    public static TaskItemDto ToDto(this TaskItem task)
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
            task.ProjectId,
            task.Project?.Name ?? string.Empty,
            task.AssigneeId,
            task.Assignee?.FullName,
            task.ReporterId,
            task.Reporter?.FullName ?? string.Empty,
            task.Comments?.Count ?? 0,
            task.Attachments?.Count ?? 0,
            null,
            task.CreatedAt);

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
            IsPrivate = dto.IsPrivate
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
    }

    public static CommentDto ToDto(this TaskComment comment)
        => new(
            comment.Id,
            comment.Content,
            comment.TaskItemId,
            comment.AuthorId,
            comment.Author?.FullName ?? string.Empty,
            comment.Author?.AvatarUrl,
            comment.CreatedAt,
            comment.UpdatedAt);
}
