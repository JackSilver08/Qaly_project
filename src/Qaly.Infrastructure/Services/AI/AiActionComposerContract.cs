using System.Globalization;
using System.Text;
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
                option.Commands == null || option.Commands.Count is < 1 or > AiActionComposerContract.MaximumTaskCommands ||
                (snapshot.RequestedTaskCount.HasValue && option.Commands.Count != snapshot.RequestedTaskCount.Value) ||
                option.Commands.Select(command => command.CommandId).Distinct(StringComparer.Ordinal).Count() != option.Commands.Count)
            {
                error = snapshot.RequestedTaskCount.HasValue
                    ? $"Every option must contain exactly the {snapshot.RequestedTaskCount.Value} task commands explicitly requested by the user."
                    : $"Every option requires an ID, label, summary, and one to {AiActionComposerContract.MaximumTaskCommands} uniquely identified commands.";
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
            if (!TryValidateCommandDependencies(commands, out error))
            {
                return false;
            }
            commands = ApplyRequestedAssignment(snapshot, ApplySafeSprintSchedule(snapshot, commands));

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
            AddAssignmentDisclosure(snapshot, NormalizeList(model.Warnings, 12, 300)),
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
        var parsedPrompt = AiTaskPrompt.Parse(snapshot.UserIntent);
        var intent = string.IsNullOrWhiteSpace(snapshot.TaskContent) ? parsedPrompt.Content.Trim() : snapshot.TaskContent.Trim();
        if (string.IsNullOrWhiteSpace(intent)) intent = "Phạm vi công việc cần được người dùng bổ sung";
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
        var explicitTitles = parsedPrompt.TaskTitles(snapshot.RequestedTaskCount);
        var explicitCommands = explicitTitles.Count > 0
            ? explicitTitles.Select((title, index) => BuildFallbackCommand(
                $"fallback-request-{index + 1:D2}", title,
                isVietnamese ? $"Thực hiện và bàn giao: {title}." : $"Deliver and verify: {title}.",
                [isVietnamese ? $"{title} đáp ứng phạm vi đã duyệt" : $"{title} meets the reviewed scope"],
                "High", 4, fallbackSourceRefs)).ToList()
            : null;
        var commands = BuildNamedTenTaskFallback(snapshot, fallbackSourceRefs) ?? explicitCommands ?? (isVietnamese
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
            });

        var desiredCommandCount = snapshot.RequestedTaskCount ?? commands.Count;
        if (desiredCommandCount < commands.Count)
        {
            commands = commands.Take(desiredCommandCount).ToList();
        }
        while (commands.Count < desiredCommandCount)
        {
            commands.Add(BuildSupplementalFallbackCommand(
                commands.Count + 1,
                isVietnamese,
                intent,
                fallbackSourceRefs));
        }
        commands = ApplyRequestedAssignment(snapshot, ApplySafeSprintSchedule(snapshot, commands));

        var option = new AiActionOptionDto(
            "server-safe-plan",
            isVietnamese ? "Phương án an toàn để duyệt" : "Safe review plan",
            isVietnamese
                ? "Qaly đã lập hạn theo Sprint và chỉ đề xuất người khi có đủ bằng chứng kỹ năng, availability và capacity đa dự án; việc chưa đủ bằng chứng vẫn để chưa giao."
                : "Qaly scheduled due dates within the Sprint and only suggested assignees backed by skill evidence, availability, and cross-project capacity; unverified work remains unassigned.",
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
            AddAssignmentDisclosure(snapshot, isVietnamese
                ? ["Model đã chọn không trả được schema hợp lệ; Qaly dùng bản nháp server an toàn để luồng không bị gián đoạn."]
                : ["The selected model did not return a valid schema; Qaly used a safe server draft so the workflow can continue."]),
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
            review.SelectedCommandIds.Count is < 1 or > AiActionComposerContract.MaximumTaskCommands ||
            review.SelectedCommandIds.Distinct(StringComparer.Ordinal).Count() != review.SelectedCommandIds.Count)
        {
            error = $"Reviewed action plan must select one option and one to {AiActionComposerContract.MaximumTaskCommands} unique commands.";
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
        if (!TryValidateCommandDependencies(selected, out error))
        {
            return false;
        }
        if (!TryValidateSystemSuggestedCapacity(selected, snapshot, out error))
        {
            return false;
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
            snapshot.RequestedTaskCount is < 1 or > AiActionComposerContract.MaximumTaskCommands ||
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
        var requestedMemberId = snapshot.RequestedAssigneeId;
        if (requestedMemberId.HasValue &&
            (snapshot.LeaveUnassigned || snapshot.Members.All(member => member.UserId != requestedMemberId.Value)))
        {
            error = "Requested assignee must be an authorized Project member and cannot contradict unassigned mode.";
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
        if (!allowUserSelected && snapshot.RequestedAssigneeId.HasValue &&
            !AiTaskPrompt.Parse(snapshot.UserIntent).TaskTitles(snapshot.RequestedTaskCount).Contains(command.Title) &&
            (AiTaskPrompt.Parse(command.Title ?? string.Empty).Assignee != null ||
             AiTaskPrompt.Parse(command.Description ?? string.Empty).Assignee != null))
        {
            error = "The model copied assignment instructions into task content; business content and assignee must be separate.";
            return false;
        }
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
                !allowUserSelected ||
                assigneeMode is not ("user_selected" or "system_suggested"))
            {
                error = allowUserSelected
                    ? "Assignee is outside the authorized project or was not explicitly selected by the reviewer."
                    : "The model cannot assign a task from workload alone; leave it unassigned for review.";
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

        var dependencyCommandIds = (command.DependencyCommandIds ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (dependencyCommandIds.Count > AiActionComposerContract.MaximumTaskCommands - 1 ||
            dependencyCommandIds.Any(item => string.Equals(item, commandId, StringComparison.Ordinal)))
        {
            error = "Task dependencies must be unique and cannot reference the same command.";
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
            sourceRefs,
            dependencyCommandIds);
        return true;
    }

    private static List<AiActionTaskCommandDto> ApplyRequestedAssignment(
        AiActionContextSnapshotDto snapshot, List<AiActionTaskCommandDto> commands)
    {
        if (snapshot.LeaveUnassigned)
            return commands.Select(command => command with { AssigneeId = null, AssigneeMode = "unassigned" }).ToList();
        if (!snapshot.RequestedAssigneeId.HasValue) return commands;
        // The model cannot select people. This ID came from an explicit user instruction,
        // resolved against active Project members by the server, just like a review selection.
        return commands.Select(command => command with
        {
            AssigneeId = snapshot.RequestedAssigneeId,
            AssigneeMode = "user_selected"
        }).ToList();
    }

    private static IReadOnlyList<string> AddAssignmentDisclosure(AiActionContextSnapshotDto snapshot, IReadOnlyList<string> warnings)
        => snapshot.RequestedAssigneeId.HasValue
            ? warnings.Append($"Người được giao theo chỉ định: {snapshot.RequestedAssigneeName}. Đây là lựa chọn để review, không phải kết luận AI đã xác minh đủ kỹ năng/capacity. Bạn có thể đổi người trước khi xác nhận.").ToArray()
            : warnings;

    private static List<AiActionTaskCommandDto> ApplySafeSprintSchedule(
        AiActionContextSnapshotDto snapshot,
        List<AiActionTaskCommandDto> commands)
    {
        if (snapshot.Sprint == null || commands.Count == 0)
            return commands.ToList();

        var today = DateTimeOffset.UtcNow.Date;
        var windowStart = snapshot.Sprint.StartDate > today ? snapshot.Sprint.StartDate : today;
        var windowEnd = snapshot.Sprint.EndDate;
        if (windowEnd < windowStart)
            return commands.ToList();

        var remainingByMember = snapshot.Members.ToDictionary(
            member => member.UserId,
            member => member.RemainingCapacityHours ?? 0m);
        var totalHours = Math.Max(1, commands.Sum(command => command.EstimatedHours ?? 1));
        var elapsedHours = 0;
        var dueByCommand = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        var scheduled = new List<AiActionTaskCommandDto>(commands.Count);

        // Schedule in dependency order so a model may return cards in any visual order
        // without allowing a successor to receive an earlier deadline than its blocker.
        var commandById = commands.ToDictionary(command => command.CommandId, StringComparer.Ordinal);
        var orderedCommands = new List<AiActionTaskCommandDto>(commands.Count);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        void AddWithDependencies(AiActionTaskCommandDto command)
        {
            if (!visited.Add(command.CommandId)) return;
            foreach (var dependencyId in command.DependencyCommandIds ?? [])
                AddWithDependencies(commandById[dependencyId]);
            orderedCommands.Add(command);
        }
        foreach (var command in commands)
            AddWithDependencies(command);

        foreach (var command in orderedCommands)
        {
            elapsedHours += command.EstimatedHours ?? 1;
            var ratio = Math.Clamp((double)elapsedHours / totalHours, 0d, 1d);
            var spanDays = Math.Max(0, (windowEnd.Date - windowStart.Date).Days);
            var due = MoveToBusinessDay(windowStart.Date.AddDays((int)Math.Round(spanDays * ratio)), windowEnd);
            var predecessorDue = (command.DependencyCommandIds ?? [])
                .Where(dueByCommand.ContainsKey)
                .Select(id => dueByCommand[id])
                .DefaultIfEmpty(windowStart)
                .Max();
            if (due < predecessorDue) due = predecessorDue;
            if (due > windowEnd) due = windowEnd;
            dueByCommand[command.CommandId] = due;

            var estimate = command.EstimatedHours ?? 0;
            var requiredSkills = (command.RequiredSkills ?? [])
                .Select(skill => skill.SkillId)
                .ToHashSet();
            var candidate = snapshot.Members
                .Where(member => IsDeclaredCapacity(member) && member.IsAvailableForSprint)
                .Where(member => remainingByMember.GetValueOrDefault(member.UserId) >= estimate)
                .Where(member => requiredSkills.IsSubsetOf((member.VerifiedSkillIds ?? []).ToHashSet()))
                .OrderByDescending(member => remainingByMember.GetValueOrDefault(member.UserId))
                .ThenBy(member => member.ActiveTaskCount)
                .ThenBy(member => member.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (candidate != null)
                remainingByMember[candidate.UserId] -= estimate;

            scheduled.Add(command with
            {
                DueDate = due,
                AssigneeId = candidate?.UserId,
                AssigneeMode = candidate == null ? "unassigned" : "system_suggested"
            });
        }

        var scheduledById = scheduled.ToDictionary(command => command.CommandId, StringComparer.Ordinal);
        return commands.Select(command => scheduledById[command.CommandId]).ToList();
    }

    private static bool TryValidateSystemSuggestedCapacity(
        IReadOnlyList<AiActionTaskCommandDto> commands,
        AiActionContextSnapshotDto snapshot,
        out string? error)
    {
        var members = snapshot.Members.ToDictionary(member => member.UserId);
        var consumed = new Dictionary<Guid, decimal>();
        foreach (var command in commands.Where(command =>
                     command.AssigneeId.HasValue && command.AssigneeMode == "system_suggested"))
        {
            var member = members[command.AssigneeId!.Value];
            var requiredSkills = (command.RequiredSkills ?? []).Select(skill => skill.SkillId).ToHashSet();
            if (!IsDeclaredCapacity(member) || !member.IsAvailableForSprint ||
                !requiredSkills.IsSubsetOf((member.VerifiedSkillIds ?? []).ToHashSet()))
            {
                error = "A Qaly-suggested assignee no longer has declared capacity, Sprint availability, or verified skill evidence.";
                return false;
            }

            consumed[member.UserId] = consumed.GetValueOrDefault(member.UserId) + (command.EstimatedHours ?? 0);
            if (consumed[member.UserId] > (member.RemainingCapacityHours ?? 0m))
            {
                error = "A Qaly-suggested assignment exceeds the member's remaining cross-project capacity.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsDeclaredCapacity(AiActionMemberContextDto member)
        => member.CapacityState is "declared" or "declared_with_availability";

    private static DateTimeOffset MoveToBusinessDay(DateTimeOffset candidate, DateTimeOffset windowEnd)
    {
        while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday && candidate < windowEnd)
            candidate = candidate.AddDays(1);
        return candidate > windowEnd ? windowEnd : candidate;
    }

    private static List<AiActionTaskCommandDto>? BuildNamedTenTaskFallback(
        AiActionContextSnapshotDto snapshot,
        IReadOnlyList<string> sourceRefs)
    {
        if (snapshot.RequestedTaskCount != 10) return null;
        var normalized = NormalizeForMatching(snapshot.UserIntent);
        var requiredSignals = new[]
        {
            "khao sat", "user flow", "ui kit", "api contract", "database",
            "auth", "booking", "payment", "e2e", "tai lieu van hanh"
        };
        if (requiredSignals.Count(signal => normalized.Contains(signal, StringComparison.Ordinal)) < 8) return null;

        IReadOnlyList<AiActionSkillSelectionDto> Skill(params string[] signals)
        {
            var match = snapshot.Skills.FirstOrDefault(skill =>
            {
                var haystack = NormalizeForMatching($"{skill.Name} {skill.Description}");
                return signals.Any(signal => haystack.Contains(signal, StringComparison.Ordinal));
            });
            return match == null ? [] : [new AiActionSkillSelectionDto(match.SkillId, "Proficient")];
        }

        return
        [
            BuildFallbackCommand("p11-research", "Khảo sát người dùng và bối cảnh nghiệp vụ",
                "Thu thập nhu cầu, pain point, vai trò và ràng buộc của luồng dịch vụ.",
                ["Có biên bản khảo sát và nguồn tham chiếu", "Pain point, vai trò và unknown được phân loại"],
                "High", 6, sourceRefs, Skill("business", "analysis", "product")),
            BuildFallbackCommand("p11-user-flow", "Thiết kế user flow end-to-end",
                "Mô tả happy path, ngoại lệ và navigation từ đăng nhập đến hoàn tất dịch vụ.",
                ["Happy path và ngoại lệ có điểm bắt đầu/kết thúc rõ", "Mỗi trạng thái có điều hướng kế tiếp"],
                "High", 6, sourceRefs, Skill("ux", "product", "analysis"), ["p11-research"]),
            BuildFallbackCommand("p11-ui-kit", "Xây dựng UI kit và trạng thái giao diện",
                "Chuẩn hóa component, token, trạng thái loading/empty/error và responsive behavior.",
                ["Component và token dùng lại được", "Có trạng thái loading, empty, error và responsive"],
                "Medium", 8, sourceRefs, Skill("frontend", "vue", "ui", "ux"), ["p11-user-flow"]),
            BuildFallbackCommand("p11-api-contract", "Định nghĩa API contract",
                "Chốt request/response, validation, authorization và error contract cho luồng chính.",
                ["Contract có schema và mã lỗi", "Authorization và validation được mô tả"],
                "High", 8, sourceRefs, Skill("backend", "api", ".net"), ["p11-user-flow"]),
            BuildFallbackCommand("p11-database", "Thiết kế database và migration",
                "Thiết kế dữ liệu canonical, constraint, index và migration an toàn.",
                ["Schema có constraint và index cần thiết", "Migration có phương án rollback"],
                "High", 8, sourceRefs, Skill("database", "sql", "ef core"), ["p11-api-contract"]),
            BuildFallbackCommand("p11-auth", "Triển khai xác thực và phân quyền",
                "Triển khai đăng ký/đăng nhập, session và authorization theo vai trò.",
                ["Luồng xác thực chạy với dữ liệu thật", "Truy cập trái quyền bị từ chối và được kiểm thử"],
                "Critical", 10, sourceRefs, Skill("security", "auth", "backend"), ["p11-api-contract", "p11-database"]),
            BuildFallbackCommand("p11-booking", "Triển khai luồng booking dịch vụ và phòng",
                "Cho phép tìm, chọn, giữ chỗ và xác nhận booking với kiểm tra xung đột.",
                ["Không thể double-book cùng tài nguyên", "Trạng thái booking nhất quán end-to-end"],
                "Critical", 14, sourceRefs, Skill("backend", "frontend", "vue", ".net"), ["p11-auth", "p11-database"]),
            BuildFallbackCommand("p11-payment", "Tích hợp thanh toán và đối soát",
                "Xử lý payment intent, callback, retry và idempotency cho booking.",
                ["Retry không tạo giao dịch trùng", "Trạng thái thanh toán và booking được đối soát"],
                "Critical", 12, sourceRefs, Skill("payment", "backend", "security"), ["p11-booking"]),
            BuildFallbackCommand("p11-e2e", "Kiểm thử E2E luồng dịch vụ",
                "Kiểm thử các luồng chính và lỗi từ đăng nhập, booking đến thanh toán.",
                ["Happy path chạy qua dữ liệu canonical", "Các lỗi auth, booking và payment có assertion"],
                "High", 10, sourceRefs, Skill("qa", "playwright", "test"), ["p11-ui-kit", "p11-auth", "p11-booking", "p11-payment"]),
            BuildFallbackCommand("p11-ops-docs", "Hoàn thiện tài liệu vận hành",
                "Viết runbook triển khai, monitor, xử lý sự cố và rollback.",
                ["Runbook có owner và bước kiểm tra", "Có hướng dẫn monitor, incident và rollback"],
                "Medium", 6, sourceRefs, Skill("devops", "operations", "documentation"), ["p11-e2e"])
        ];
    }

    private static bool TryValidateCommandDependencies(
        IReadOnlyList<AiActionTaskCommandDto> commands,
        out string? error)
    {
        var ids = commands.Select(item => item.CommandId).ToHashSet(StringComparer.Ordinal);
        if (commands.Any(command => (command.DependencyCommandIds ?? []).Any(dependency =>
                !ids.Contains(dependency) || string.Equals(dependency, command.CommandId, StringComparison.Ordinal))))
        {
            error = "Every task dependency must reference another selected command in the same option.";
            return false;
        }

        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        bool Visit(string id)
        {
            if (state.GetValueOrDefault(id) == 1) return true;
            if (state.GetValueOrDefault(id) == 2) return false;
            state[id] = 1;
            var command = commands.First(item => string.Equals(item.CommandId, id, StringComparison.Ordinal));
            foreach (var dependency in command.DependencyCommandIds ?? [])
                if (Visit(dependency)) return true;
            state[id] = 2;
            return false;
        }
        if (commands.Any(command => Visit(command.CommandId)))
        {
            error = "Task dependency graph contains a cycle.";
            return false;
        }

        error = null;
        return true;
    }

    private static string NormalizeForMatching(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd');
    }

    private static AiActionTaskCommandDto BuildFallbackCommand(
        string commandId,
        string title,
        string description,
        IReadOnlyList<string> acceptanceCriteria,
        string priority,
        int estimatedHours,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<AiActionSkillSelectionDto>? requiredSkills = null,
        IReadOnlyList<string>? dependencyCommandIds = null)
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
            requiredSkills ?? [],
            sourceRefs,
            dependencyCommandIds ?? []);

    private static AiActionTaskCommandDto BuildSupplementalFallbackCommand(
        int ordinal,
        bool isVietnamese,
        string intent,
        IReadOnlyList<string> sourceRefs)
    {
        var viPhases = new (string Title, string Description, string Acceptance)[]
        {
            ("Phân tích luồng nghiệp vụ và hành trình người dùng", "Mô hình hóa luồng nghiệp vụ, vai trò và các điểm chuyển trạng thái cần thiết.", "Luồng chính, ngoại lệ và vai trò được mô tả rõ"),
            ("Thiết kế kiến trúc và ranh giới tích hợp", "Xác định các thành phần, hợp đồng dữ liệu và điểm tích hợp cho phạm vi đã thống nhất.", "Kiến trúc và hợp đồng tích hợp có thể được review"),
            ("Thiết kế trải nghiệm và giao diện", "Chuẩn bị cấu trúc màn hình, trạng thái giao diện và hành vi điều hướng.", "Màn hình, trạng thái và navigation được đặc tả"),
            ("Xây dựng mô hình dữ liệu và migration", "Thiết kế dữ liệu canonical, ràng buộc và migration cần thiết.", "Dữ liệu và ràng buộc được kiểm chứng"),
            ("Phát triển backend và API", "Triển khai business logic, validation và API cho phạm vi đã duyệt.", "API đáp ứng business rules và có kiểm thử"),
            ("Phát triển frontend và tương tác", "Triển khai giao diện, trạng thái và xử lý lỗi theo thiết kế.", "Luồng frontend hoạt động với dữ liệu thật"),
            ("Tích hợp end-to-end", "Kết nối các thành phần và kiểm tra luồng hoàn chỉnh trên môi trường tích hợp.", "Luồng end-to-end chạy không có dead-end"),
            ("Kiểm tra bảo mật, hiệu năng và khả năng phục hồi", "Rà quyền truy cập, tải, retry và các tình huống lỗi quan trọng.", "Các rủi ro chính được kiểm tra và ghi nhận"),
            ("Chuẩn bị phát hành và vận hành", "Hoàn thiện checklist phát hành, quan sát và phương án rollback.", "Có checklist phát hành, monitor và rollback"),
            ("Theo dõi sau phát hành và cải tiến", "Đo kết quả thực tế, xử lý vấn đề và cập nhật backlog cải tiến.", "Kết quả sau phát hành được đo và phản hồi vào backlog")
        };
        var enPhases = new (string Title, string Description, string Acceptance)[]
        {
            ("Analyze business flow and user journeys", "Model the business flow, roles, transitions, and important exceptions.", "Primary flows, exceptions, and roles are documented"),
            ("Design architecture and integration boundaries", "Define components, data contracts, and integration boundaries for the agreed scope.", "Architecture and integration contracts are reviewable"),
            ("Design experience and interface states", "Prepare screen structure, UI states, and navigation behavior.", "Screens, states, and navigation are specified"),
            ("Build canonical data model and migrations", "Design canonical data, constraints, and required migrations.", "Data and constraints are verified"),
            ("Implement backend and APIs", "Implement business logic, validation, and APIs for the approved scope.", "APIs enforce business rules and are tested"),
            ("Implement frontend interactions", "Build the UI, state transitions, and error handling from the design.", "Frontend flows work with real data"),
            ("Integrate the end-to-end flow", "Connect components and verify the complete flow in an integration environment.", "The end-to-end flow has no dead ends"),
            ("Verify security, performance, and resilience", "Check authorization, load, retries, and critical failure paths.", "Primary risks are tested and recorded"),
            ("Prepare release and operations", "Complete release checks, observability, and rollback guidance.", "Release, monitoring, and rollback checklists exist"),
            ("Monitor launch and improve", "Measure real outcomes, address issues, and update the improvement backlog.", "Post-launch outcomes feed the backlog")
        };
        var phases = isVietnamese ? viPhases : enPhases;
        var phase = phases[(ordinal - 4) % phases.Length];
        var cycle = (ordinal - 4) / phases.Length + 1;
        var suffix = cycle > 1 ? $" ({cycle})" : string.Empty;
        return BuildFallbackCommand(
            $"fallback-step-{ordinal:D2}",
            $"{phase.Title}{suffix}",
            $"{phase.Description} {(isVietnamese ? "Yêu cầu gốc" : "Original request")}: {intent}",
            [phase.Acceptance, isVietnamese ? "Kết quả có bằng chứng và sẵn sàng để nghiệm thu" : "The outcome has evidence and is ready for acceptance"],
            ordinal <= 10 ? "High" : "Medium",
            ordinal is 8 or 9 ? 8 : 4,
            sourceRefs);
    }

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
