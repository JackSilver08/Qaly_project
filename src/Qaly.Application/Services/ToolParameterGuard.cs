using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;

namespace Qaly.Application.Services;

public class ToolParameterGuard
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<TaskItem> _taskRepo;
    private readonly IRepository<ProjectMember> _memberRepo;
    private readonly IRepository<User> _userRepo;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITaskAccessPolicy _taskAccessPolicy;

    public ToolParameterGuard(
        IRepository<Project> projectRepo,
        IRepository<TaskItem> taskRepo,
        IRepository<ProjectMember> memberRepo,
        IRepository<User> userRepo,
        ICurrentUserService currentUserService,
        ITaskAccessPolicy taskAccessPolicy)
    {
        _projectRepo = projectRepo;
        _taskRepo = taskRepo;
        _memberRepo = memberRepo;
        _userRepo = userRepo;
        _currentUserService = currentUserService;
        _taskAccessPolicy = taskAccessPolicy;
    }

    public async Task<ToolResult> GuardAsync(string toolName, Dictionary<string, object?> parameters, CancellationToken ct = default)
    {
        var currentUserId = _currentUserService.UserId;
        if (currentUserId == null || currentUserId == Guid.Empty)
        {
            return new ToolResult
            {
                Success = false,
                ErrorCode = "UNAUTHORIZED",
                UserMessage = "Bạn cần đăng nhập để thực hiện tác vụ này.",
                RetryHint = "The user is not authenticated. Abort the tool call and ask user to log in."
            };
        }

        bool isAdmin = ProjectRoleRules.IsSystemAdmin(_currentUserService.Role);

        // 1. Extract and Validate Project Context
        Guid? projectId = null;
        if (parameters.TryGetValue("projectId", out var projVal) && projVal != null)
        {
            if (!TryGetGuid(projVal, out var parsedProjId))
            {
                return InvalidParameterResult("projectId", "Giá trị projectId không hợp lệ.", "projectId must be a valid Guid string.");
            }
            projectId = parsedProjId;
        }

        // 2. Extract and Validate Task Context
        Guid? taskId = null;
        TaskItem? task = null;
        if (parameters.TryGetValue("taskId", out var taskVal) && taskVal != null)
        {
            if (!TryGetGuid(taskVal, out var parsedTaskId))
            {
                return InvalidParameterResult("taskId", "Giá trị taskId không hợp lệ.", "taskId must be a valid Guid string.");
            }
            taskId = parsedTaskId;

            task = await _taskRepo.GetQueryable()
                .AsNoTracking()
                .Include(item => item.Project)
                    .ThenInclude(project => project.Organization)
                .Include(item => item.Assignees)
                .FirstOrDefaultAsync(t => t.Id == taskId.Value, ct);

            if (task == null || !await _taskAccessPolicy.CanAccessTaskAsync(task, ct))
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorCode = "TASK_NOT_FOUND",
                    UserMessage = "Không tìm thấy công việc hoặc bạn không có quyền xem công việc này.",
                    RetryHint = "The task is absent or not visible to the requester. Do not disclose whether it exists."
                };
            }

            // If projectId is not explicitly provided, infer it from task
            if (!projectId.HasValue)
            {
                projectId = task.ProjectId;
            }
            else if (task.ProjectId != projectId.Value)
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorCode = "TASK_PROJECT_MISMATCH",
                    UserMessage = "Công việc không thuộc dự án đã chọn.",
                    RetryHint = $"The task with ID {taskId} belongs to projectId {task.ProjectId}, which does not match the provided projectId {projectId.Value}."
                };
            }
        }

        if (RequiresTask(toolName) && task == null)
        {
            return InvalidParameterResult(
                "taskId",
                "Vui lòng cung cấp công việc cần thao tác.",
                "taskId is required for this task-scoped tool.");
        }

        // 3. Project Access & Role Check
        string? projectRole = null;
        var canManageProject = false;
        if (projectId.HasValue)
        {
            var project = await _projectRepo.GetQueryable()
                .AsNoTracking()
                .Include(item => item.Organization)
                .FirstOrDefaultAsync(p => p.Id == projectId.Value, ct);

            if (project == null)
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorCode = "PROJECT_NOT_FOUND",
                    UserMessage = "Không tìm thấy dự án đã chọn.",
                    RetryHint = $"The projectId {projectId.Value} does not exist in the database. Ask the user for a valid projectId."
                };
            }

            if (!await _taskAccessPolicy.CanAccessProjectAsync(project.Id, project.OwnerId, ct))
            {
                return new ToolResult
                {
                    Success = false,
                    ErrorCode = "FORBIDDEN",
                    UserMessage = "Bạn không có quyền truy cập dự án này.",
                    RetryHint = "The project is not visible to the requester. Abort the tool call without disclosing project data."
                };
            }

            canManageProject = await _taskAccessPolicy.CanManageProjectAsync(project.Id, project.OwnerId, ct);
            if (isAdmin || project.OwnerId == currentUserId.Value || canManageProject)
            {
                projectRole = ProjectRoleRules.Owner;
            }
            else
            {
                var member = await _memberRepo.GetQueryable()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.ProjectId == projectId.Value && m.UserId == currentUserId.Value, ct);

                projectRole = member?.Role;
            }
        }
        else if (RequiresProject(toolName))
        {
            return new ToolResult
            {
                Success = false,
                ErrorCode = "PROJECT_REQUIRED",
                UserMessage = "Vui lòng chọn hoặc cung cấp dự án để thực hiện câu lệnh này.",
                RetryHint = "projectId is required for this tool. Please specify projectId in parameters."
            };
        }

        bool isPM = canManageProject;

        // 4. Role-based Tool Permission Verification
        var isWriteTool = IsWriteAction(toolName);
        if (isWriteTool)
        {
            // PM and Admin can write anything. Members can write comments or time tracking.
            if (!isPM && !isAdmin)
            {
                if (toolName is "StartTimeTracking" or "StopTimeTracking" or "AddComment")
                {
                    // Normal members can start/stop time tracking or add comments on tasks they have access to.
                }
                else if (toolName == "UpdateTaskStatus")
                {
                    // normal member can only update task status if they are the assignee
                    if (task == null ||
                        (task.AssigneeId != currentUserId.Value &&
                         !task.Assignees.Any(assignment => assignment.UserId == currentUserId.Value)))
                    {
                        return new ToolResult
                        {
                            Success = false,
                            ErrorCode = "UNAUTHORIZED_ACTION",
                            UserMessage = "Bạn chỉ có thể cập nhật trạng thái của công việc được phân công cho mình.",
                            RetryHint = $"The user is a project member, not PM/Admin, and is not the assignee of task {taskId}. Only the assignee or a PM can update status."
                        };
                    }
                }
                else
                {
                    return new ToolResult
                    {
                        Success = false,
                        ErrorCode = "FORBIDDEN_ACTION",
                        UserMessage = "Bạn không có quyền thực hiện hành động chỉnh sửa này. Chỉ PM hoặc Admin mới có quyền.",
                        RetryHint = $"User role in project is '{projectRole}'. Writing tasks requires Manager/Owner role. Abort and inform user."
                    };
                }
            }
        }

        // 5. Tool-Specific Parameter Validations
        switch (toolName)
        {
            case "CreateTask":
                if (parameters.TryGetValue("title", out var title) && string.IsNullOrWhiteSpace(title?.ToString()))
                {
                    return InvalidParameterResult("title", "Tiêu đề công việc không được để trống.", "title parameter is required and cannot be empty.");
                }
                if (parameters.TryGetValue("priority", out var priority) && priority != null)
                {
                    var pStr = priority.ToString()!;
                    if (!IsValidPriority(pStr))
                    {
                        return InvalidParameterResult("priority", "Độ ưu tiên không hợp lệ.", "priority must be one of: Low, Medium, High, Critical.");
                    }
                }
                if (parameters.TryGetValue("assigneeId", out var assId) && assId != null)
                {
                    if (TryGetGuid(assId, out var assigneeGuid))
                    {
                        var isMember = await _memberRepo.GetQueryable().AnyAsync(m => m.ProjectId == projectId!.Value && m.UserId == assigneeGuid, ct);
                        if (!isMember)
                        {
                            return InvalidParameterResult("assigneeId", "Người được phân công không thuộc dự án này.", "assigneeId must be a member of the specified project.");
                        }
                    }
                }
                break;

            case "UpdateTaskStatus":
                if (parameters.TryGetValue("status", out var status) && status != null)
                {
                    var sStr = status.ToString()!;
                    if (!IsValidStatus(sStr))
                    {
                        return InvalidParameterResult("status", "Trạng thái công việc không hợp lệ.", "status must be one of: Todo, InProgress, InReview, Done, Cancelled.");
                    }
                }
                else
                {
                    return InvalidParameterResult("status", "Trạng thái công việc là bắt buộc.", "status parameter is required.");
                }
                break;

            case "AssignTask":
                if (parameters.TryGetValue("assigneeId", out var assId2) && assId2 != null)
                {
                    if (TryGetGuid(assId2, out var assigneeGuid))
                    {
                        var isMember = await _memberRepo.GetQueryable().AnyAsync(m => m.ProjectId == projectId!.Value && m.UserId == assigneeGuid, ct);
                        if (!isMember)
                        {
                            return InvalidParameterResult("assigneeId", "Người được phân công không thuộc dự án này.", "assigneeId must be a member of the specified project.");
                        }
                    }
                    else
                    {
                        return InvalidParameterResult("assigneeId", "assigneeId không hợp lệ.", "assigneeId must be a valid Guid string.");
                    }
                }
                else
                {
                    return InvalidParameterResult("assigneeId", "assigneeId là bắt buộc.", "assigneeId is required.");
                }
                break;

            case "SetTaskPriority":
                if (parameters.TryGetValue("priority", out var pVal) && pVal != null)
                {
                    var pStr = pVal.ToString()!;
                    if (!IsValidPriority(pStr))
                    {
                        return InvalidParameterResult("priority", "Độ ưu tiên không hợp lệ.", "priority must be one of: Low, Medium, High, Critical.");
                    }
                }
                else
                {
                    return InvalidParameterResult("priority", "Độ ưu tiên là bắt buộc.", "priority parameter is required.");
                }
                break;

            case "AddComment":
                if (parameters.TryGetValue("content", out var content) && string.IsNullOrWhiteSpace(content?.ToString()))
                {
                    return InvalidParameterResult("content", "Nội dung bình luận không được để trống.", "content parameter is required and cannot be empty.");
                }
                break;

            case "StopTimeTracking":
                if (parameters.TryGetValue("entryId", out var entryVal) && entryVal != null)
                {
                    if (!TryGetGuid(entryVal, out _))
                    {
                        return InvalidParameterResult("entryId", "Mã ghi nhận thời gian không hợp lệ.", "entryId must be a valid Guid string.");
                    }
                }
                else
                {
                    return InvalidParameterResult("entryId", "Mã ghi nhận thời gian là bắt buộc.", "entryId is required.");
                }
                break;
        }

        return new ToolResult { Success = true };
    }

    private static bool RequiresProject(string toolName)
    {
        return toolName switch
        {
            "GetProjectSummary" => true,
            "GetOverdueTasks" => true,
            "CreateTask" => true,
            "GetMemberWorkload" => true,
            "SearchKnowledge" => true,
            "GenerateExcelReport" => true,
            "GenerateWordReport" => true,
            "GetMyTimeLogs" => true,
            "SuggestTaskAssignment" => true,
            _ => false
        };
    }

    private static bool IsWriteAction(string toolName)
    {
        return toolName switch
        {
            "CreateTask" => true,
            "UpdateTaskStatus" => true,
            "AssignTask" => true,
            "SetTaskPriority" => true,
            "AddDueDate" => true,
            "AddComment" => true,
            "StartTimeTracking" => true,
            "StopTimeTracking" => true,
            _ => false
        };
    }

    private static bool RequiresTask(string toolName)
        => toolName is "UpdateTaskStatus" or "AssignTask" or "SetTaskPriority" or
            "AddDueDate" or "AddComment" or "StartTimeTracking";

    private static bool TryGetGuid(object value, out Guid guid)
    {
        guid = Guid.Empty;
        if (value is Guid g)
        {
            guid = g;
            return true;
        }
        if (value is string s && Guid.TryParse(s, out var parsed))
        {
            guid = parsed;
            return true;
        }
        return false;
    }

    private static bool IsValidPriority(string priority)
    {
        return string.Equals(priority, "Low", StringComparison.OrdinalIgnoreCase)
               || string.Equals(priority, "Medium", StringComparison.OrdinalIgnoreCase)
               || string.Equals(priority, "High", StringComparison.OrdinalIgnoreCase)
               || string.Equals(priority, "Critical", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidStatus(string status)
    {
        return string.Equals(status, "Todo", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "InReview", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase)
               || string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase);
    }

    private static ToolResult InvalidParameterResult(string parameterName, string userMsg, string hint)
    {
        return new ToolResult
        {
            Success = false,
            ErrorCode = "INVALID_PARAMETER",
            UserMessage = userMsg,
            RetryHint = $"Parameter '{parameterName}' is invalid: {hint}"
        };
    }
}
