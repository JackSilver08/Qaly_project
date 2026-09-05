using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Seeds;

public partial class DataSeeder
{
    internal const string DefenseWikiTitle = "Kịch bản phản biện workflow quản lý dự án";
    internal const string DefenseUnassignedTitle = "DEMO-QA 01 · Task mới chờ phân công";
    internal const string DefenseInProgressTitle = "DEMO-QA 02 · Đang thực hiện có time log";
    internal const string DefenseOnHoldTitle = "DEMO-QA 03 · Tạm dừng vì phụ thuộc bên ngoài";
    internal const string DefenseInReviewTitle = "DEMO-QA 04 · Chờ reviewer duyệt evidence";
    internal const string DefenseReturnedTitle = "DEMO-QA 05 · Reviewer đã trả lại để sửa";
    internal const string DefenseDoneTitle = "DEMO-QA 06 · Hoàn thành sau duyệt hợp lệ";
    internal const string DefenseCancelledTitle = "DEMO-QA 07 · Hủy vì thay đổi phạm vi";

    /// <summary>
    /// Adds a compact, queryable set of canonical task/review states for a thesis
    /// defense. Existing rows are never reset, so a manual demo may continue from
    /// the state it reached; only missing baseline relations are replenished.
    /// </summary>
    private async Task<bool> EnsureProjectManagementDefenseDemoSeedAsync()
    {
        var project = await _context.Projects
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Code == "qaly-workos-demo" && !item.IsDeleted);
        if (project?.OrganizationId == null)
        {
            return false;
        }

        var users = await _context.Users
            .Where(item => item.IsActive && item.Email.EndsWith("@qaly.dev"))
            .ToDictionaryAsync(item => item.Email, StringComparer.OrdinalIgnoreCase);
        string[] requiredEmails =
        [
            "admin@qaly.dev", "minh.anh@qaly.dev", "bao.ngoc@qaly.dev",
            "quoc.huy@qaly.dev", "tuan.kiet@qaly.dev", "thanh.tam@qaly.dev"
        ];
        if (requiredEmails.Any(email => !users.ContainsKey(email)))
        {
            return false;
        }

        var sprint = await _context.Set<Sprint>()
            .Where(item => item.ProjectId == project.Id && item.Status == "Active")
            .OrderByDescending(item => item.StartDate)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync();
        if (sprint == null)
        {
            return false;
        }

        User U(string email) => users[email];
        var now = DateTimeOffset.UtcNow;
        var changed = false;
        if (!project.EnableOnHold || !project.EnableInReview || !project.RequireEvidenceToDone || project.RestrictTransitionsToAdmin)
        {
            project.EnableOnHold = true;
            project.EnableInReview = true;
            project.RequireEvidenceToDone = true;
            project.RestrictTransitionsToAdmin = false;
            project.UpdatedAt = now;
            changed = true;
        }

        var scenarioTitles = new[]
        {
            DefenseUnassignedTitle, DefenseInProgressTitle, DefenseOnHoldTitle,
            DefenseInReviewTitle, DefenseReturnedTitle, DefenseDoneTitle, DefenseCancelledTitle
        };
        var tasks = await _context.TaskItems
            .IgnoreQueryFilters()
            .Where(item => item.ProjectId == project.Id && !item.IsDeleted && scenarioTitles.Contains(item.Title))
            .ToDictionaryAsync(item => item.Title, StringComparer.Ordinal);

        TaskItem EnsureTask(
            string title,
            string description,
            string status,
            string priority,
            string? assigneeEmail,
            string reporterEmail,
            string? reviewerEmail,
            int startOffset,
            int dueOffset,
            int estimate,
            int? actual,
            int sortOrder)
        {
            if (tasks.TryGetValue(title, out var existing))
            {
                return existing;
            }

            var task = new TaskItem
            {
                ProjectId = project.Id,
                SprintId = sprint.Id,
                Title = title,
                Description = description,
                Status = status,
                Priority = priority,
                AssigneeId = assigneeEmail == null ? null : U(assigneeEmail).Id,
                ReporterId = U(reporterEmail).Id,
                ReviewerId = reviewerEmail == null ? null : U(reviewerEmail).Id,
                StartDate = now.AddDays(startOffset),
                DueDate = now.AddDays(dueOffset),
                EstimatedHours = estimate,
                ActualHours = actual,
                SortOrder = sortOrder,
                CreatedAt = now.AddDays(startOffset),
                UpdatedAt = status is "Done" or "Cancelled" ? now.AddDays(Math.Min(-1, dueOffset)) : now.AddHours(-8)
            };
            _context.TaskItems.Add(task);
            if (task.AssigneeId.HasValue)
            {
                _context.TaskAssignments.Add(new TaskAssignment
                {
                    TaskItemId = task.Id,
                    UserId = task.AssigneeId.Value,
                    AssignedByUserId = task.ReporterId,
                    AssignedAt = task.CreatedAt.AddHours(2),
                    CreatedAt = task.CreatedAt.AddHours(2)
                });
            }
            tasks[title] = task;
            changed = true;
            return task;
        }

        var unassigned = EnsureTask(
            DefenseUnassignedTitle,
            "Tình huống chứng minh Qaly không tự gán người khi chưa đối chiếu đủ skill evidence, availability và capacity đã khai báo.",
            "Todo", "High", null, "minh.anh@qaly.dev", null, -1, 5, 8, null, 110);
        var inProgress = EnsureTask(
            DefenseInProgressTitle,
            "Task đã được giao hợp lệ, có estimate, time log và checklist đang làm dở để giải thích cách theo dõi tiến độ thực tế.",
            "InProgress", "High", "quoc.huy@qaly.dev", "minh.anh@qaly.dev", "thanh.tam@qaly.dev", -4, 3, 16, 6, 120);
        var onHold = EnsureTask(
            DefenseOnHoldTitle,
            "Tạm dừng có lý do vì khách hàng chưa bàn giao API sandbox; đây không phải hoàn thành và vẫn được tính là công việc mở/quá hạn.",
            "OnHold", "Critical", "tuan.kiet@qaly.dev", "minh.anh@qaly.dev", "thanh.tam@qaly.dev", -6, -1, 12, 4, 130);
        var inReview = EnsureTask(
            DefenseInReviewTitle,
            "Assignee đã gửi duyệt; reviewer khác assignee phải xem checklist và evidence Pending trước khi quyết định Done hoặc trả lại.",
            "InReview", "High", "quoc.huy@qaly.dev", "minh.anh@qaly.dev", "thanh.tam@qaly.dev", -5, 1, 14, 12, 140);
        var returned = EnsureTask(
            DefenseReturnedTitle,
            "Reviewer đã từ chối evidence thiếu log lỗi và trả Task từ InReview về InProgress; assignee phải sửa rồi gửi duyệt lại.",
            "InProgress", "High", "tuan.kiet@qaly.dev", "minh.anh@qaly.dev", "thanh.tam@qaly.dev", -7, 2, 10, 8, 150);
        var done = EnsureTask(
            DefenseDoneTitle,
            "Task chỉ hoàn thành sau khi checklist đạt, evidence được Approved và reviewer có quyền xác nhận; attribution kỹ năng lấy từ kết quả này.",
            "Done", "High", "quoc.huy@qaly.dev", "minh.anh@qaly.dev", "thanh.tam@qaly.dev", -10, -3, 12, 11, 160);
        var cancelled = EnsureTask(
            DefenseCancelledTitle,
            "Phạm vi này bị loại khỏi Sprint sau quyết định đổi scope; Cancelled không được tính là hoàn thành hay đóng góp tiến độ.",
            "Cancelled", "Low", null, "minh.anh@qaly.dev", null, -8, -4, 6, null, 170);

        await SaveIfChangedAsync();

        var checklistDefinitions = new (TaskItem Task, string Text, int SortOrder, bool Completed)[]
        {
            (unassigned, "Xác nhận đầu ra và tiêu chí nghiệm thu với PM", 0, false),
            (unassigned, "Chọn người có skill evidence và capacity phù hợp", 1, false),
            (unassigned, "Đặt hạn nằm trong Sprint và sau dependency", 2, false),
            (inProgress, "API trả đúng contract đã duyệt", 0, true),
            (inProgress, "Có unit test cho happy path", 1, true),
            (inProgress, "Có test lỗi quyền truy cập", 2, false),
            (onHold, "Nhận API key sandbox từ khách hàng", 0, false),
            (onHold, "Chạy smoke test kết nối sandbox", 1, false),
            (inReview, "Luồng chính chạy qua môi trường demo", 0, true),
            (inReview, "Không có lỗi Critical/High còn mở", 1, true),
            (inReview, "Evidence hiển thị đủ request, response và timestamp", 2, true),
            (inReview, "Reviewer xác nhận độc lập với assignee", 3, false),
            (returned, "Bổ sung log lỗi và correlation ID", 0, false),
            (returned, "Chạy lại regression sau khi sửa", 1, false),
            (returned, "Gửi evidence mới để reviewer duyệt lại", 2, false),
            (done, "Luồng chính chạy qua môi trường demo", 0, true),
            (done, "Regression và phân quyền đều PASS", 1, true),
            (done, "Evidence đã được reviewer Approved", 2, true),
            (done, "Canonical read-back khớp sau reload", 3, true)
        };
        var scenarioTaskIds = tasks.Values.Select(item => item.Id).ToHashSet();
        var checklistKeys = (await _context.TaskAcceptanceChecklistItems
                .Where(item => scenarioTaskIds.Contains(item.TaskId))
                .Select(item => new { item.TaskId, item.SortOrder })
                .ToListAsync())
            .Select(item => (item.TaskId, item.SortOrder))
            .ToHashSet();
        foreach (var definition in checklistDefinitions.Where(item => !checklistKeys.Contains((item.Task.Id, item.SortOrder))))
        {
            _context.TaskAcceptanceChecklistItems.Add(new TaskAcceptanceChecklistItem
            {
                TaskId = definition.Task.Id,
                Text = definition.Text,
                SortOrder = definition.SortOrder,
                IsCompleted = definition.Completed,
                CreatedByUserId = U("minh.anh@qaly.dev").Id,
                CreatedAt = definition.Task.CreatedAt.AddHours(3)
            });
            changed = true;
        }

        var skills = await _context.OrganizationSkills
            .Where(item => item.OrganizationId == project.OrganizationId.Value && item.IsActive)
            .ToDictionaryAsync(item => item.NormalizedName, StringComparer.OrdinalIgnoreCase);
        var skillDefinitions = new (TaskItem Task, string Skill, string Level)[]
        {
            (unassigned, "qa-test-engineering", "Advanced"),
            (inProgress, "backend-dotnet", "Working"),
            (onHold, "data-retail-integration", "Working"),
            (inReview, "qa-playwright", "Advanced"),
            (returned, "devops-observability", "Working"),
            (done, "security-auth-privacy", "Advanced")
        };
        var requirementKeys = (await _context.TaskSkillRequirements
                .Where(item => scenarioTaskIds.Contains(item.TaskItemId))
                .Select(item => new { item.TaskItemId, item.OrganizationSkillId })
                .ToListAsync())
            .Select(item => (item.TaskItemId, item.OrganizationSkillId))
            .ToHashSet();
        foreach (var definition in skillDefinitions)
        {
            if (!skills.TryGetValue(definition.Skill, out var skill) ||
                !requirementKeys.Add((definition.Task.Id, skill.Id)))
            {
                continue;
            }
            _context.TaskSkillRequirements.Add(new TaskSkillRequirement
            {
                TaskItemId = definition.Task.Id,
                OrganizationSkillId = skill.Id,
                RequiredLevel = definition.Level,
                Provenance = "MANUAL",
                ConfirmedByUserId = U("minh.anh@qaly.dev").Id,
                ConfirmedAt = definition.Task.CreatedAt.AddHours(1),
                CreatedAt = definition.Task.CreatedAt.AddHours(1)
            });
            changed = true;
        }

        var dependencyDefinitions = new[]
        {
            (Predecessor: done.Id, Successor: inProgress.Id),
            (Predecessor: inProgress.Id, Successor: unassigned.Id)
        };
        var dependencyKeys = (await _context.TaskDependencies
                .Where(item => scenarioTaskIds.Contains(item.PredecessorId) && scenarioTaskIds.Contains(item.SuccessorId))
                .Select(item => new { item.PredecessorId, item.SuccessorId })
                .ToListAsync())
            .Select(item => (item.PredecessorId, item.SuccessorId))
            .ToHashSet();
        foreach (var dependency in dependencyDefinitions.Where(item => dependencyKeys.Add((item.Predecessor, item.Successor))))
        {
            _context.TaskDependencies.Add(new TaskDependency
            {
                PredecessorId = dependency.Predecessor,
                SuccessorId = dependency.Successor,
                DependencyType = "FinishToStart",
                CreatedAt = now.AddDays(-3)
            });
            changed = true;
        }

        var commentDefinitions = new (TaskItem Task, string Author, string Content, DateTimeOffset CreatedAt)[]
        {
            (onHold, "tuan.kiet@qaly.dev", "Blocker: khách hàng chưa cấp API key sandbox. Không có credential thật nên Qaly giữ OnHold và không giả lập kết nối thành công.", now.AddDays(-2)),
            (returned, "thanh.tam@qaly.dev", "Review trả lại: evidence chưa có correlation ID và chưa chứng minh lỗi 403 được xử lý. Hãy bổ sung rồi gửi duyệt lại.", now.AddDays(-1)),
            (inReview, "quoc.huy@qaly.dev", "Đã hoàn tất checklist kỹ thuật và gửi evidence; đang chờ reviewer độc lập xác nhận.", now.AddHours(-12)),
            (done, "thanh.tam@qaly.dev", "Đã đối chiếu checklist, evidence và read-back sau reload; đủ điều kiện xác nhận Done.", now.AddDays(-3))
        };
        foreach (var definition in commentDefinitions)
        {
            var exists = await _context.TaskComments.AnyAsync(item =>
                item.TaskItemId == definition.Task.Id && item.Content == definition.Content);
            if (exists) continue;
            _context.TaskComments.Add(new TaskComment
            {
                TaskItemId = definition.Task.Id,
                AuthorId = U(definition.Author).Id,
                Content = definition.Content,
                CreatedAt = definition.CreatedAt
            });
            changed = true;
        }

        var evidenceDefinitions = new (TaskItem Task, string FileName, string Hash, string Status, string Uploader, string? Reviewer, string? Note, DateTimeOffset UploadedAt)[]
        {
            (inReview, "demo-review-pending.json", "demo_defense_review_pending_v1", "Pending", "quoc.huy@qaly.dev", null, null, now.AddHours(-14)),
            (returned, "demo-review-rejected.json", "demo_defense_review_rejected_v1", "Rejected", "tuan.kiet@qaly.dev", "thanh.tam@qaly.dev", "Thiếu correlation ID và bằng chứng xử lý 403.", now.AddDays(-2)),
            (done, "demo-review-approved.json", "demo_defense_review_approved_v1", "Approved", "quoc.huy@qaly.dev", "thanh.tam@qaly.dev", "Checklist đạt, evidence đủ nguồn và read-back khớp.", now.AddDays(-4))
        };
        foreach (var definition in evidenceDefinitions)
        {
            if (await _context.TaskAttachments.IgnoreQueryFilters().AnyAsync(item =>
                    item.TaskItemId == definition.Task.Id && item.FileName == definition.FileName))
            {
                continue;
            }

            var physicalFile = await _context.PhysicalFiles.SingleOrDefaultAsync(item => item.ContentHash == definition.Hash);
            var attachment = new TaskAttachment
            {
                TaskItemId = definition.Task.Id,
                UploadedById = U(definition.Uploader).Id,
                FileName = definition.FileName,
                ContentType = "application/json",
                Scope = "Task",
                IsEvidence = true,
                EvidenceApprovalStatus = definition.Status,
                EvidenceReviewedById = definition.Reviewer == null ? null : U(definition.Reviewer).Id,
                EvidenceReviewedAt = definition.Reviewer == null ? null : definition.UploadedAt.AddHours(4),
                EvidenceReviewNote = definition.Note,
                UploadedAt = definition.UploadedAt,
                CreatedAt = definition.UploadedAt
            };
            if (physicalFile == null)
            {
                attachment.PhysicalFile = new PhysicalFile
                {
                    ContentHash = definition.Hash,
                    FilePath = $"/uploads/demo/defense/{definition.FileName}",
                    FileSize = 4_096,
                    ReferenceCount = 1
                };
            }
            else
            {
                attachment.PhysicalFileId = physicalFile.Id;
            }
            _context.TaskAttachments.Add(attachment);
            changed = true;
        }

        // These tasks were created by earlier versions of the official rich/presentation
        // seed, before evidence approval became mandatory. Backfill only the named seed
        // rows; never fabricate approval for a user-created Done task.
        var seededDoneBaselines = new[]
        {
            new
            {
                Title = "Rà soát matrix phân quyền cho manager và viewer",
                FileName = "demo-role-matrix-approved.json",
                Hash = "demo_role_matrix_approved_v1",
                Note = "Đã đối chiếu ma trận quyền Manager/Viewer và xác nhận kết quả."
            },
            new
            {
                Title = "Chốt API contract Release 4.0",
                FileName = "demo-api-contract-approved.json",
                Hash = "demo_api_contract_approved_v1",
                Note = "Đã đối chiếu API contract với acceptance criteria và xác nhận read-back."
            },
            new
            {
                Title = "Kiểm thử phân quyền Manager và Member",
                FileName = "demo-access-control-approved.json",
                Hash = "demo_access_control_approved_v1",
                Note = "Đã kiểm tra luồng Manager/Member và xác nhận không có quyền vượt scope."
            }
        };
        foreach (var baseline in seededDoneBaselines)
        {
            var baselineTask = await _context.TaskItems.IgnoreQueryFilters().SingleOrDefaultAsync(item =>
                item.ProjectId == project.Id && !item.IsDeleted && item.Status == "Done" &&
                item.Title == baseline.Title);
            if (baselineTask == null || await _context.TaskAttachments.IgnoreQueryFilters().AnyAsync(item =>
                    item.TaskItemId == baselineTask.Id && item.IsEvidence &&
                    item.EvidenceApprovalStatus == "Approved" && !item.IsDeleted))
            {
                continue;
            }

            var existingPhysicalFile = await _context.PhysicalFiles
                .SingleOrDefaultAsync(item => item.ContentHash == baseline.Hash);
            var baselineAttachment = new TaskAttachment
            {
                TaskItemId = baselineTask.Id,
                UploadedById = baselineTask.AssigneeId ?? U("admin@qaly.dev").Id,
                FileName = baseline.FileName,
                ContentType = "application/json",
                Scope = "Task",
                IsEvidence = true,
                EvidenceApprovalStatus = "Approved",
                EvidenceReviewedById = baselineTask.ReviewerId ?? U("thanh.tam@qaly.dev").Id,
                EvidenceReviewedAt = now.AddDays(-2),
                EvidenceReviewNote = baseline.Note,
                UploadedAt = now.AddDays(-3),
                CreatedAt = now.AddDays(-3)
            };
            if (existingPhysicalFile == null)
            {
                baselineAttachment.PhysicalFile = new PhysicalFile
                {
                    ContentHash = baseline.Hash,
                    FilePath = $"/uploads/demo/defense/{baseline.FileName}",
                    FileSize = 4_096,
                    ReferenceCount = 1
                };
            }
            else
            {
                baselineAttachment.PhysicalFileId = existingPhysicalFile.Id;
            }
            _context.TaskAttachments.Add(baselineAttachment);
            changed = true;
        }

        if (!await _context.TimeEntries.AnyAsync(item => item.TaskId == inProgress.Id && item.Note == "Demo time log cho workflow quản lý dự án."))
        {
            _context.TimeEntries.Add(new TimeEntry
            {
                TaskId = inProgress.Id,
                UserId = U("quoc.huy@qaly.dev").Id,
                StartedAt = now.AddDays(-2).AddHours(-4),
                ManualMinutes = 210,
                Note = "Demo time log cho workflow quản lý dự án.",
                CreatedAt = now.AddDays(-2).AddHours(-4)
            });
            changed = true;
        }

        var doneContributorId = U("quoc.huy@qaly.dev").Id;
        if (!await _context.TaskCompletionAttributions.AnyAsync(item =>
                item.TaskItemId == done.Id && item.ContributorUserId == doneContributorId))
        {
            _context.TaskCompletionAttributions.Add(new TaskCompletionAttribution
            {
                TaskItemId = done.Id,
                ContributorUserId = doneContributorId,
                ConfirmedByUserId = U("thanh.tam@qaly.dev").Id,
                CompletedAt = now.AddDays(-3),
                ConfirmedAt = now.AddDays(-3).AddHours(2),
                Status = TaskCompletionAttribution.Confirmed,
                AttributionPolicyVersion = "completion-contributor.v1",
                CreatedAt = now.AddDays(-3).AddHours(2)
            });
            changed = true;
        }

        var auditDefinitions = new (TaskItem Task, string Action, string Changes, DateTimeOffset Timestamp)[]
        {
            (inReview, "TaskStatusChanged", "{\"from\":\"InProgress\",\"to\":\"InReview\",\"reason\":\"submitted_for_review\"}", now.AddHours(-13)),
            (returned, "EvidenceReviewed", "{\"status\":\"Rejected\",\"reason\":\"missing_correlation_id\"}", now.AddDays(-1).AddHours(-2)),
            (returned, "TaskStatusChanged", "{\"from\":\"InReview\",\"to\":\"InProgress\",\"reason\":\"changes_requested\"}", now.AddDays(-1)),
            (done, "EvidenceReviewed", "{\"status\":\"Approved\",\"reason\":\"acceptance_verified\"}", now.AddDays(-3).AddHours(1)),
            (done, "TaskStatusChanged", "{\"from\":\"InReview\",\"to\":\"Done\",\"reason\":\"review_approved\"}", now.AddDays(-3).AddHours(2)),
            (cancelled, "TaskStatusChanged", "{\"from\":\"Todo\",\"to\":\"Cancelled\",\"reason\":\"scope_removed\"}", now.AddDays(-4))
        };
        foreach (var definition in auditDefinitions)
        {
            if (await _context.AuditLogs.AnyAsync(item =>
                    item.EntityType == nameof(TaskItem) && item.EntityId == definition.Task.Id.ToString() &&
                    item.Action == definition.Action && item.ChangesJson == definition.Changes))
            {
                continue;
            }
            _context.AuditLogs.Add(new AuditLog
            {
                Action = definition.Action,
                EntityType = nameof(TaskItem),
                EntityId = definition.Task.Id.ToString(),
                ChangesJson = definition.Changes,
                UserId = U("thanh.tam@qaly.dev").Id,
                Timestamp = definition.Timestamp,
                IpAddress = "127.0.0.1"
            });
            changed = true;
        }

        var notificationDefinitions = new[]
        {
            new Notification
            {
                UserId = U("thanh.tam@qaly.dev").Id,
                Message = $"Task '{DefenseInReviewTitle}' đang chờ bạn duyệt evidence.",
                Type = "TaskReviewRequested", Tone = "info", RelatedEntityType = "Task",
                RelatedEntityId = inReview.Id, IdempotencyKey = "demo:defense:review-requested", IsRead = false,
                CreatedAt = now.AddHours(-12)
            },
            new Notification
            {
                UserId = U("tuan.kiet@qaly.dev").Id,
                Message = $"Task '{DefenseReturnedTitle}' đã được trả lại để bổ sung evidence.",
                Type = "TaskChangesRequested", Tone = "warning", RelatedEntityType = "Task",
                RelatedEntityId = returned.Id, IdempotencyKey = "demo:defense:changes-requested", IsRead = false,
                CreatedAt = now.AddDays(-1)
            }
        };
        foreach (var notification in notificationDefinitions)
        {
            if (await _context.Notifications.AnyAsync(item =>
                    item.UserId == notification.UserId && item.IdempotencyKey == notification.IdempotencyKey))
            {
                continue;
            }
            _context.Notifications.Add(notification);
            changed = true;
        }

        if (!await _context.WikiPages.IgnoreQueryFilters().AnyAsync(item =>
                item.ProjectId == project.Id && !item.IsDeleted && item.Title == DefenseWikiTitle))
        {
            _context.WikiPages.Add(new WikiPage
            {
                ProjectId = project.Id,
                AuthorId = U("admin@qaly.dev").Id,
                Title = DefenseWikiTitle,
                Visibility = "internal",
                Content = $$"""
                    # Bộ dữ liệu đối đáp workflow quản lý dự án

                    1. **Vì sao Member không được kéo thẳng sang Hoàn thành?**
                       Project bật InReview và yêu cầu evidence. Member gửi `{{DefenseInReviewTitle}}` sang Đang duyệt; reviewer độc lập mới được quyết định.
                    2. **Nếu evidence chưa đạt thì sao?**
                       `{{DefenseReturnedTitle}}` có evidence Rejected, audit InReview → InProgress, review note và thông báo cho assignee.
                    3. **Done hợp lệ được chứng minh thế nào?**
                       `{{DefenseDoneTitle}}` có checklist hoàn tất, evidence Approved, reviewer khác assignee, attribution và audit read-back.
                    4. **Task bị nghẽn có bị tính nhầm là hoàn thành không?**
                       `{{DefenseOnHoldTitle}}` giữ OnHold, có lý do external blocker và vẫn là task mở/quá hạn.
                    5. **Qaly có tự gán người khi lịch còn trống không?**
                       Không. `{{DefenseUnassignedTitle}}` cố ý để chưa giao cho đến khi skill evidence, capacity và availability đủ.
                    6. **Scope bị cắt xử lý ra sao?**
                       `{{DefenseCancelledTitle}}` là Cancelled, không được cộng vào tiến độ hoàn thành.

                    Deep-link Project: `/projects/{{project.Id}}?tab=tasks`.
                    """,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            });
            changed = true;
        }

        await SaveIfChangedAsync();
        return changed;
    }
}
