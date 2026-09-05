using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Seeds;

public partial class DataSeeder
{
    internal const string DashboardScenarioPrefix = "DEMO-DASH · ";

    /// <summary>
    /// Keeps the named demo portfolio visually useful without rewriting user-created rows.
    /// Every active seed Project has both completed and open work, while the open workload
    /// deliberately includes a spare-capacity case and an overloaded/blocker case.
    /// </summary>
    private async Task<bool> EnsureDashboardPresentationVarietyAsync()
    {
        var organization = await _context.Organizations
            .SingleOrDefaultAsync(item => item.Code == "qaly-demo-2026" && item.IsActive);
        if (organization == null) return false;

        var projects = await _context.Projects
            .IgnoreQueryFilters()
            .Where(item => item.OrganizationId == organization.Id && !item.IsDeleted && item.Status == "Active")
            .ToDictionaryAsync(item => item.Code, StringComparer.OrdinalIgnoreCase);
        var users = await _context.Users
            .Where(item => item.IsActive && item.Email.EndsWith("@qaly.dev"))
            .ToDictionaryAsync(item => item.Email, StringComparer.OrdinalIgnoreCase);
        string[] requiredProjects = ["erumi-local-analytics", "nova-retail-pilot", "field-ops-mobile", "ops-compliance-readiness"];
        string[] requiredUsers = ["minh.anh@qaly.dev", "bao.ngoc@qaly.dev", "quoc.huy@qaly.dev", "thu.ha@qaly.dev", "gia.khang@qaly.dev", "linh.chi@qaly.dev", "tuan.kiet@qaly.dev", "mai.phuong@qaly.dev", "thanh.tam@qaly.dev", "viet.long@qaly.dev"];
        if (requiredProjects.Any(code => !projects.ContainsKey(code)) || requiredUsers.Any(email => !users.ContainsKey(email)))
            return false;

        var now = DateTimeOffset.UtcNow;
        var changed = false;
        User U(string email) => users[email];
        Project P(string code) => projects[code];

        var sprintByProject = await _context.Set<Sprint>()
            .Where(item => projects.Values.Select(project => project.Id).Contains(item.ProjectId) && item.Status == "Active")
            .GroupBy(item => item.ProjectId)
            .Select(group => group.OrderByDescending(item => item.StartDate).First())
            .ToDictionaryAsync(item => item.ProjectId);

        var definitions = new (string ProjectCode, string Title, string Description, string Status, string Priority, string Assignee, string Reviewer, int StartOffset, int DueOffset, int Hours)[]
        {
            ("erumi-local-analytics", "Chuẩn hóa truy vấn portfolio theo RBAC", "Ảnh chụp dữ liệu AI chỉ gồm Project và Task mà người hỏi được phép xem.", "Done", "High", "linh.chi@qaly.dev", "thanh.tam@qaly.dev", -18, -10, 14),
            ("erumi-local-analytics", "Đo cache hit cho câu hỏi lặp lại", "Ghi latency, cache hit và provider thực tế để giải thích hiệu năng AI trong buổi demo.", "Done", "Medium", "viet.long@qaly.dev", "thanh.tam@qaly.dev", -12, -6, 8),
            ("erumi-local-analytics", "Theo dõi fallback khi provider gián đoạn", "Giữ OnHold vì endpoint provider demo đang bảo trì; không giả lập phản hồi cloud thành công.", "OnHold", "Critical", "quoc.huy@qaly.dev", "thanh.tam@qaly.dev", -4, 2, 28),

            ("nova-retail-pilot", "Đồng bộ danh mục cho Wave 1", "Đã đối chiếu SKU, giá và tồn kho cho bốn cửa hàng đầu tiên.", "Done", "High", "quoc.huy@qaly.dev", "thanh.tam@qaly.dev", -24, -16, 18),
            ("nova-retail-pilot", "Đào tạo cửa hàng trưởng Wave 1", "Hoàn tất buổi hướng dẫn, checklist và biên bản tiếp nhận tại cửa hàng.", "Done", "Medium", "thu.ha@qaly.dev", "thanh.tam@qaly.dev", -16, -9, 10),
            ("nova-retail-pilot", "Nghiệm thu báo cáo doanh thu Wave 2", "Đã gửi dashboard, nguồn dữ liệu và checklist cho reviewer độc lập.", "InReview", "High", "mai.phuong@qaly.dev", "thanh.tam@qaly.dev", -4, 3, 6),

            ("field-ops-mobile", "Phát hành bản beta Android nội bộ", "Bản beta đã qua smoke test, ký build và xác nhận thiết bị hiện trường.", "Done", "High", "gia.khang@qaly.dev", "thanh.tam@qaly.dev", -14, -8, 16),
            ("field-ops-mobile", "Khắc phục xung đột dữ liệu offline", "Xử lý hai thiết bị cùng cập nhật checklist khi mất mạng và đồng bộ trở lại.", "InProgress", "High", "gia.khang@qaly.dev", "tuan.kiet@qaly.dev", -3, 4, 18),

            ("ops-compliance-readiness", "Duyệt chính sách lưu trữ log", "Đã xác nhận retention, phạm vi dữ liệu và chủ thể chịu trách nhiệm.", "Done", "High", "bao.ngoc@qaly.dev", "thanh.tam@qaly.dev", -20, -13, 8),
            ("ops-compliance-readiness", "Kiểm thử webhook retry và chữ ký", "Đã xác minh retry có giới hạn, idempotency và chữ ký payload.", "Done", "High", "viet.long@qaly.dev", "thanh.tam@qaly.dev", -15, -8, 12),
            ("ops-compliance-readiness", "Diễn tập yêu cầu xuất dữ liệu cá nhân", "Đã hoàn tất DSAR mẫu với audit và biên bản bàn giao nội bộ.", "Done", "Medium", "minh.anh@qaly.dev", "thanh.tam@qaly.dev", -10, -5, 8),
            ("ops-compliance-readiness", "Rà soát secret trước buổi demo", "Kiểm tra cấu hình local và thay mọi khóa thật bằng placeholder an toàn.", "Todo", "Critical", "thu.ha@qaly.dev", "thanh.tam@qaly.dev", 0, 5, 4)
        };

        var existing = await _context.TaskItems.IgnoreQueryFilters()
            .Where(item => !item.IsDeleted && item.Title.StartsWith(DashboardScenarioPrefix))
            .ToDictionaryAsync(item => item.Title, StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            var title = DashboardScenarioPrefix + definition.Title;
            if (existing.ContainsKey(title)) continue;

            var project = P(definition.ProjectCode);
            var assignee = U(definition.Assignee);
            var reviewer = U(definition.Reviewer);
            var task = new TaskItem
            {
                ProjectId = project.Id,
                SprintId = sprintByProject.GetValueOrDefault(project.Id)?.Id,
                Title = title,
                Description = definition.Description,
                Status = definition.Status,
                Priority = definition.Priority,
                AssigneeId = assignee.Id,
                ReporterId = project.OwnerId,
                ReviewerId = reviewer.Id == assignee.Id ? project.OwnerId : reviewer.Id,
                StartDate = now.AddDays(definition.StartOffset),
                DueDate = now.AddDays(definition.DueOffset),
                EstimatedHours = definition.Hours,
                ActualHours = definition.Status == "Done" ? Math.Max(1, definition.Hours - 1) : null,
                SortOrder = 300 + existing.Count * 10,
                CreatedAt = now.AddDays(definition.StartOffset),
                UpdatedAt = definition.Status == "Done" ? now.AddDays(definition.DueOffset) : now.AddHours(-4)
            };
            _context.TaskItems.Add(task);
            _context.TaskAssignments.Add(new TaskAssignment
            {
                TaskItemId = task.Id,
                UserId = assignee.Id,
                AssignedByUserId = project.OwnerId,
                AssignedAt = task.CreatedAt.AddHours(1),
                CreatedAt = task.CreatedAt.AddHours(1)
            });

            if (definition.Status == "Done")
            {
                var fileName = $"{definition.ProjectCode}-{task.Id:N}-approved.json";
                var reviewedAt = task.DueDate ?? now.AddDays(-1);
                _context.TaskAttachments.Add(new TaskAttachment
                {
                    TaskItemId = task.Id,
                    UploadedById = assignee.Id,
                    FileName = fileName,
                    ContentType = "application/json",
                    IsEvidence = true,
                    EvidenceApprovalStatus = "Approved",
                    EvidenceReviewedById = task.ReviewerId,
                    EvidenceReviewedAt = reviewedAt,
                    EvidenceReviewNote = "Đã đối chiếu với acceptance criteria và dữ liệu read-back của demo.",
                    UploadedAt = reviewedAt.AddHours(-2),
                    PhysicalFile = new PhysicalFile
                    {
                        ContentHash = $"dashboard_demo_{task.Id:N}",
                        FilePath = $"/uploads/demo/dashboard/{fileName}",
                        FileSize = 3_072,
                        ReferenceCount = 1
                    }
                });
            }

            if (definition.Status == "OnHold")
            {
                _context.TaskComments.Add(new TaskComment
                {
                    TaskItemId = task.Id,
                    AuthorId = assignee.Id,
                    Content = "Blocker đã xác nhận: provider demo bảo trì. Chờ endpoint ổn định rồi chạy lại contract test.",
                    CreatedAt = now.AddHours(-6)
                });
            }

            existing[title] = task;
            changed = true;
        }

        if (changed) await _context.SaveChangesAsync();
        return changed;
    }
}
