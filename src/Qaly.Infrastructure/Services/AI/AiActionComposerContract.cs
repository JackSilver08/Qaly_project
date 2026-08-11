using System.Text.Json;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;

namespace Qaly.Infrastructure.Services.AI;

public static class AiActionComposerOutputContract
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryBuildResult(
        string modelJson,
        string snapshotJson,
        out string resultJson,
        out string? error)
    {
        resultJson = string.Empty;
        if (!SchemaArtifactIsAvailable(out error) ||
            !TryReadSnapshot(snapshotJson, out var snapshot, out error))
        {
            return false;
        }

        AiActionPlanDto? model;
        try
        {
            model = JsonSerializer.Deserialize<AiActionPlanDto>(modelJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Action Composer response is invalid JSON: {exception.Message}";
            return false;
        }

        if (model == null)
        {
            error = "Action Composer response is empty.";
            return false;
        }

        if (!string.Equals(model.SchemaId, AiActionComposerContract.SchemaId, StringComparison.Ordinal) ||
            model.ProjectId != snapshot!.Project.Id ||
            !string.Equals(model.SourceVersion, snapshot.SourceVersion, StringComparison.Ordinal) ||
            !string.Equals(model.IntentType, "task.create", StringComparison.Ordinal) ||
            model.Confidence is < 0m or > 1m)
        {
            error = "Action Composer output does not match the authorized schema, project, source version, intent, or confidence range.";
            return false;
        }

        if (model.Options == null || model.Options.Count is < 1 or > 3 ||
            model.Options.Count > snapshot.MaximumOptions ||
            model.Options.Select(option => option.OptionId).Distinct(StringComparer.Ordinal).Count() != model.Options.Count)
        {
            error = "Action Composer must return one to three uniquely identified options within the requested maximum.";
            return false;
        }

        var allowedMembers = snapshot.Members.ToDictionary(item => item.UserId);
        var allowedSkills = snapshot.Skills.ToDictionary(item => item.SkillId);
        var allowedRefs = snapshot.AllowedSourceRefs.ToHashSet(StringComparer.Ordinal);
        var canonicalOptions = new List<AiActionOptionDto>(model.Options.Count);
        foreach (var option in model.Options)
        {
            var optionId = NormalizeText(option.OptionId, 80);
            var label = NormalizeText(option.Label, 120);
            var summary = NormalizeText(option.Summary, 500);
            if (optionId == null || label == null || summary == null ||
                option.Commands == null || option.Commands.Count is < 1 or > 5 ||
                option.Commands.Select(command => command.CommandId).Distinct(StringComparer.Ordinal).Count() != option.Commands.Count)
            {
                error = "Every option requires an ID, label, summary, and one to five uniquely identified commands.";
                return false;
            }

            var commands = new List<AiActionTaskCommandDto>(option.Commands.Count);
            foreach (var command in option.Commands)
            {
                if (!TryReconcileCommand(command, snapshot, allowedMembers, allowedSkills, allowedRefs, false, out var reconciled, out error))
                {
                    return false;
                }
                commands.Add(reconciled!);
            }

            canonicalOptions.Add(new AiActionOptionDto(
                optionId,
                label,
                summary,
                NormalizeList(option.TradeOffs, 8, 300),
                commands));
        }

        var first = canonicalOptions[0];
        var targetEntities = new List<AiActionTargetEntityDto>
        {
            new("project", snapshot.Project.Id, snapshot.Project.Name)
        };
        if (snapshot.Sprint != null)
        {
            targetEntities.Add(new AiActionTargetEntityDto("sprint", snapshot.Sprint.Id, snapshot.Sprint.Name));
        }
        var final = new AiActionPlanDto(
            AiActionComposerContract.SchemaId,
            "1.0",
            snapshot.Project.Id,
            snapshot.SourceVersion,
            snapshot.UserIntent,
            "task.create",
            Math.Round(model.Confidence, 2, MidpointRounding.AwayFromZero),
            targetEntities,
            NormalizeList(model.Assumptions, 12, 300),
            NormalizeList(model.MissingFields, 8, 120),
            NormalizeList(model.Warnings, 12, 300),
            canonicalOptions,
            new AiActionReviewSelectionDto(first.OptionId, first.Commands.Select(command => command.CommandId).ToList()),
            DateTimeOffset.UtcNow);
        resultJson = JsonSerializer.Serialize(final, JsonOptions);
        error = null;
        return true;
    }

    public static bool TryBuildDeterministicFallback(
        string snapshotJson,
        out string resultJson,
        out string? error)
    {
        resultJson = string.Empty;
        if (!SchemaArtifactIsAvailable(out error) ||
            !TryReadSnapshot(snapshotJson, out var snapshot, out error))
        {
            return false;
        }

        var isVietnamese = !string.Equals(snapshot!.Language, "en", StringComparison.OrdinalIgnoreCase);
        var intent = snapshot.UserIntent.Trim();
        var conciseIntent = intent.Length <= 140 ? intent : $"{intent[..137]}...";
        var projectRef = snapshot.Project.SourceRef;
        IReadOnlyList<string> fallbackSourceRefs = snapshot.Sprint == null
            ? [projectRef]
            : [projectRef, snapshot.Sprint.SourceRef];
        IReadOnlyList<AiActionTargetEntityDto> fallbackTargetEntities = snapshot.Sprint == null
            ? [new AiActionTargetEntityDto("project", snapshot.Project.Id, snapshot.Project.Name)]
            : [
                new AiActionTargetEntityDto("project", snapshot.Project.Id, snapshot.Project.Name),
                new AiActionTargetEntityDto("sprint", snapshot.Sprint.Id, snapshot.Sprint.Name)
            ];
        var commands = isVietnamese
            ? new List<AiActionTaskCommandDto>
            {
                BuildFallbackCommand(
                    "fallback-discovery",
                    "Khảo sát yêu cầu và tài liệu liên quan",
                    $"Làm rõ phạm vi và thu thập tài liệu cho yêu cầu: {intent}",
                    ["Phạm vi và nguồn tham khảo được ghi lại", "Các điểm chưa rõ và rủi ro được liệt kê"],
                    "High",
                    4,
                    fallbackSourceRefs),
                BuildFallbackCommand(
                    "fallback-delivery",
                    $"Triển khai: {conciseIntent}",
                    $"Thực hiện phạm vi đã được người dùng yêu cầu: {intent}",
                    ["Kết quả đáp ứng phạm vi đã thống nhất", "Thay đổi có thể được kiểm tra trên dữ liệu thật"],
                    "High",
                    8,
                    fallbackSourceRefs),
                BuildFallbackCommand(
                    "fallback-acceptance",
                    "Kiểm thử, nghiệm thu và cập nhật tài liệu",
                    "Kiểm tra kết quả end-to-end, ghi nhận lỗi còn lại và cập nhật tài liệu bàn giao.",
                    ["Luồng chính được kiểm thử end-to-end", "Lỗi còn lại và hướng xử lý được ghi nhận"],
                    "Medium",
                    4,
                    fallbackSourceRefs)
            }
            : new List<AiActionTaskCommandDto>
            {
                BuildFallbackCommand(
                    "fallback-discovery",
                    "Research requirements and supporting material",
                    $"Clarify scope and collect supporting material for: {intent}",
                    ["Scope and source material are documented", "Unknowns and risks are listed"],
                    "High",
                    4,
                    fallbackSourceRefs),
                BuildFallbackCommand(
                    "fallback-delivery",
                    $"Deliver: {conciseIntent}",
                    $"Implement the user-requested scope: {intent}",
                    ["The agreed scope is implemented", "The change can be verified against real data"],
                    "High",
                    8,
                    fallbackSourceRefs),
                BuildFallbackCommand(
                    "fallback-acceptance",
                    "Test, accept and document the outcome",
                    "Verify the end-to-end result, record remaining defects, and update handover documentation.",
                    ["The main flow is verified end to end", "Remaining defects and next actions are recorded"],
                    "Medium",
                    4,
                    fallbackSourceRefs)
            };

        var option = new AiActionOptionDto(
            "server-safe-plan",
            isVietnamese ? "Phương án an toàn để duyệt" : "Safe review plan",
            isVietnamese
                ? "Qaly đã tạo bản nháp có thể chỉnh sửa từ đúng yêu cầu gốc; chưa tự gán người hoặc kỹ năng khi thiếu bằng chứng."
                : "Qaly created an editable draft from the original request and did not assign people or skills without evidence.",
            isVietnamese
                ? ["Cần duyệt nội dung, Sprint, người phụ trách và kỹ năng trước khi xác nhận."]
                : ["Review content, Sprint, assignees, and skills before confirmation."],
            commands);
        var plan = new AiActionPlanDto(
            AiActionComposerContract.SchemaId,
            "1.0",
            snapshot.Project.Id,
            snapshot.SourceVersion,
            snapshot.UserIntent,
            "task.create",
            0.72m,
            fallbackTargetEntities,
            [],
            isVietnamese ? ["Sprint", "Người phụ trách", "Kỹ năng bắt buộc"] : ["Sprint", "Assignee", "Required skills"],
            isVietnamese
                ? ["Model đã chọn không trả được schema hợp lệ; Qaly dùng bản nháp server an toàn để luồng không bị gián đoạn."]
                : ["The selected model did not return a valid schema; Qaly used a safe server draft so the workflow can continue."],
            [option],
            new AiActionReviewSelectionDto(option.OptionId, commands.Select(command => command.CommandId).ToList()),
            DateTimeOffset.UtcNow);
        resultJson = JsonSerializer.Serialize(plan, JsonOptions);
        return TryValidateFinal(resultJson, out error);
    }

    public static bool TryValidateReviewedPlan(
        string payloadJson,
        AiActionContextSnapshotDto snapshot,
        out AiActionPlanDto? plan,
        out string? error)
    {
        plan = null;
        error = null;
        try
        {
            plan = JsonSerializer.Deserialize<AiActionPlanDto>(payloadJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Reviewed action plan is invalid JSON: {exception.Message}";
            return false;
        }

        if (plan == null ||
            !string.Equals(plan.SchemaId, AiActionComposerContract.SchemaId, StringComparison.Ordinal) ||
            plan.ProjectId != snapshot.Project.Id ||
            !string.Equals(plan.SourceVersion, snapshot.SourceVersion, StringComparison.Ordinal) ||
            !string.Equals(plan.IntentType, "task.create", StringComparison.Ordinal) ||
            plan.Review == null ||
            plan.Options == null)
        {
            error = "Reviewed action plan changed a protected schema, project, source, or intent field.";
            return false;
        }

        var parsedPlan = plan;
        var review = parsedPlan.Review;
        var option = parsedPlan.Options.FirstOrDefault(item =>
            string.Equals(item.OptionId, review.SelectedOptionId, StringComparison.Ordinal));
        if (option == null || review.SelectedCommandIds == null ||
            review.SelectedCommandIds.Count is < 1 or > 5 ||
            review.SelectedCommandIds.Distinct(StringComparer.Ordinal).Count() != review.SelectedCommandIds.Count)
        {
            error = "Reviewed action plan must select one option and one to five unique commands.";
            return false;
        }

        var selected = option.Commands
            .Where(command => review.SelectedCommandIds.Contains(command.CommandId, StringComparer.Ordinal))
            .ToList();
        if (selected.Count != review.SelectedCommandIds.Count)
        {
            error = "Reviewed action plan selected an unknown command.";
            return false;
        }

        var allowedMembers = snapshot.Members.ToDictionary(item => item.UserId);
        var allowedSkills = snapshot.Skills.ToDictionary(item => item.SkillId);
        var allowedRefs = snapshot.AllowedSourceRefs.ToHashSet(StringComparer.Ordinal);
        foreach (var command in selected)
        {
            if (!TryReconcileCommand(command, snapshot, allowedMembers, allowedSkills, allowedRefs, true, out _, out error))
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryReadSnapshot(
        string snapshotJson,
        out AiActionContextSnapshotDto? snapshot,
        out string? error)
    {
        snapshot = null;
        error = null;
        try
        {
            snapshot = JsonSerializer.Deserialize<AiActionContextSnapshotDto>(snapshotJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = $"Action Composer snapshot is invalid JSON: {exception.Message}";
            return false;
        }

        if (snapshot == null ||
            !string.Equals(snapshot.SchemaId, AiActionComposerContract.SnapshotSchemaId, StringComparison.Ordinal) ||
            snapshot.Project == null || snapshot.Project.Id == Guid.Empty ||
            string.IsNullOrWhiteSpace(snapshot.Project.Name) ||
            string.IsNullOrWhiteSpace(snapshot.SourceVersion) ||
            string.IsNullOrWhiteSpace(snapshot.UserIntent) ||
            snapshot.UserIntent.Length > 4000 ||
            snapshot.MaximumOptions is < 1 or > 3 ||
            snapshot.Members == null || snapshot.Skills == null || snapshot.AllowedSourceRefs == null ||
            (snapshot.Sprint != null &&
                (snapshot.Sprint.Id == Guid.Empty ||
                 string.IsNullOrWhiteSpace(snapshot.Sprint.Name) ||
                 string.IsNullOrWhiteSpace(snapshot.Sprint.SourceRef) ||
                 !snapshot.AllowedSourceRefs.Contains(snapshot.Sprint.SourceRef, StringComparer.Ordinal))) ||
            snapshot.Members.Select(item => item.UserId).Distinct().Count() != snapshot.Members.Count ||
            snapshot.Skills.Select(item => item.SkillId).Distinct().Count() != snapshot.Skills.Count)
        {
            error = "Action Composer snapshot is missing an authoritative project, source version, intent, members, or skills.";
            return false;
        }

        return true;
    }

    public static bool TryValidateFinal(string resultJson, out string? error)
    {
        error = null;
        if (!SchemaArtifactIsAvailable(out error)) return false;
        try
        {
            var plan = JsonSerializer.Deserialize<AiActionPlanDto>(resultJson, JsonOptions);
            if (plan == null ||
                !string.Equals(plan.SchemaId, AiActionComposerContract.SchemaId, StringComparison.Ordinal) ||
                plan.ProjectId == Guid.Empty ||
                string.IsNullOrWhiteSpace(plan.SourceVersion) ||
                plan.Options == null || plan.Options.Count is < 1 or > 3 ||
                plan.Review == null || plan.GeneratedAt == default)
            {
                error = "Action Composer result is incomplete.";
                return false;
            }
            return true;
        }
        catch (JsonException exception)
        {
            error = $"Action Composer result is invalid JSON: {exception.Message}";
            return false;
        }
    }

    private static bool TryReconcileCommand(
        AiActionTaskCommandDto command,
        AiActionContextSnapshotDto snapshot,
        Dictionary<Guid, AiActionMemberContextDto> allowedMembers,
        Dictionary<Guid, AiActionSkillContextDto> allowedSkills,
        HashSet<string> allowedRefs,
        bool allowUserSelected,
        out AiActionTaskCommandDto? reconciled,
        out string? error)
    {
        reconciled = null;
        error = null;
        var commandId = NormalizeText(command.CommandId, 80);
        var title = NormalizeText(command.Title, 200);
        var description = NormalizeOptionalText(command.Description, 4000);
        if (commandId == null || title == null ||
            !string.Equals(command.ToolName, AiActionComposerContract.TaskCreateTool, StringComparison.Ordinal) ||
            !string.Equals(command.ToolVersion, "1.0", StringComparison.Ordinal))
        {
            error = "Every command must have an ID, title, and registered task.create.v1 tool contract.";
            return false;
        }

        var priority = TaskStatusRules.IsValidPriority(command.Priority)
            ? TaskStatusRules.NormalizePriority(command.Priority)
            : null;
        if (priority == null || command.EstimatedHours is < 1 or > 1000)
        {
            error = "Task priority or estimated hours is invalid.";
            return false;
        }
        if (command.DueDate.HasValue && command.DueDate.Value < DateTimeOffset.UtcNow.AddDays(-1))
        {
            error = "Task due date cannot be in the past.";
            return false;
        }

        var assigneeMode = command.AssigneeMode?.Trim().ToLowerInvariant() ?? "unassigned";
        if (command.AssigneeId.HasValue)
        {
            if (!allowedMembers.ContainsKey(command.AssigneeId.Value) ||
                assigneeMode is not ("workload_only" or "user_selected") ||
                (!allowUserSelected && assigneeMode == "user_selected"))
            {
                error = "Assignee is outside the authorized project or uses unsupported evidence.";
                return false;
            }
        }
        else if (assigneeMode != "unassigned")
        {
            error = "An unassigned task must use assigneeMode=unassigned.";
            return false;
        }

        var skills = new List<AiActionSkillSelectionDto>();
        foreach (var selection in command.RequiredSkills ?? [])
        {
            if (!allowedSkills.ContainsKey(selection.SkillId) ||
                !TaskSkillService.TryNormalizeLevel(selection.RequiredLevel, out var level))
            {
                error = "A required skill is absent, inactive, or has an invalid level.";
                return false;
            }
            skills.Add(new AiActionSkillSelectionDto(selection.SkillId, level));
        }
        if (skills.Select(item => item.SkillId).Distinct().Count() != skills.Count || skills.Count > 10)
        {
            error = "Required skills must be unique and cannot exceed ten rows.";
            return false;
        }

        var sourceRefs = (command.SourceRefs ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (sourceRefs.Count is < 1 or > 12 ||
            sourceRefs.Any(reference => !allowedRefs.Contains(reference)) ||
            !sourceRefs.Contains(snapshot.Project.SourceRef, StringComparer.Ordinal) ||
            (snapshot.Sprint != null && !sourceRefs.Contains(snapshot.Sprint.SourceRef, StringComparer.Ordinal)))
        {
            error = "Every task command must cite only authorized sources and include the project source.";
            return false;
        }

        reconciled = new AiActionTaskCommandDto(
            commandId,
            AiActionComposerContract.TaskCreateTool,
            "1.0",
            title,
            description,
            NormalizeList(command.AcceptanceCriteria, 12, 500),
            priority,
            command.DueDate,
            command.EstimatedHours,
            command.AssigneeId,
            assigneeMode,
            skills,
            sourceRefs);
        return true;
    }

    private static AiActionTaskCommandDto BuildFallbackCommand(
        string commandId,
        string title,
        string description,
        IReadOnlyList<string> acceptanceCriteria,
        string priority,
        int estimatedHours,
        IReadOnlyList<string> sourceRefs)
        => new(
            commandId,
            AiActionComposerContract.TaskCreateTool,
            "1.0",
            title.Length <= 200 ? title : $"{title[..197]}...",
            description.Length <= 4000 ? description : $"{description[..3997]}...",
            acceptanceCriteria,
            priority,
            null,
            estimatedHours,
            null,
            "unassigned",
            [],
            sourceRefs);

    private static List<string> NormalizeList(IReadOnlyList<string>? values, int maxCount, int maxLength)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => value.Length <= maxLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();

    private static string? NormalizeText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength ? null : normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return normalized.Length <= maxLength ? normalized : null;
    }

    private static bool SchemaArtifactIsAvailable(out string? error)
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "ai", "ai_action_intent_envelope.schema.json");
        var workspacePath = Path.Combine(Directory.GetCurrentDirectory(), "docs", "schemas", "ai", "ai_action_intent_envelope.schema.json");
        var path = File.Exists(outputPath) ? outputPath : workspacePath;
        if (!File.Exists(path))
        {
            error = "ai_action_intent_envelope.v1 schema artifact is missing.";
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("$id", out var id) ||
                !string.Equals(id.GetString(), AiActionComposerContract.SchemaId, StringComparison.Ordinal))
            {
                error = "Action Composer schema artifact has the wrong $id.";
                return false;
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            error = $"Action Composer schema artifact cannot be read: {exception.Message}";
            return false;
        }
        error = null;
        return true;
    }
}
