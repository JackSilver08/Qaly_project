using System.Globalization;
using System.Text;
using System.Text.Json;
using Qaly.Application.DTOs.Ai;

namespace Qaly.Application.Services;

public static class AiAssistantGoalPlanningOutputContract
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> AllowedDispositions = new(StringComparer.Ordinal)
    {
        "answerable", "plannable", "clarification_required", "unsupported_but_analyzed", "policy_blocked"
    };

    public static bool TryValidateModel(string content, string? validationContextJson, out string? error)
    {
        error = null;
        if (!TryDeserialize(content, out var envelope, out error) || envelope == null) return false;
        if (!string.Equals(envelope.SchemaId, AiAssistantGoalPlanningContract.SchemaId, StringComparison.Ordinal))
        {
            error = "Goal analysis contract identity is invalid.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(envelope.Objective) || envelope.Objective.Length > 1000 ||
            string.IsNullOrWhiteSpace(envelope.UserJob) || envelope.UserJob.Length > 600 ||
            envelope.IntentFacets.Count > 8 || (envelope.Scopes.Count > 0 && envelope.Scopes.Count > 4) ||
            envelope.Unknowns.Count > 8 || envelope.RankedSkills.Count > 8 || envelope.MissingSkills.Count > 8 ||
            !AllowedDispositions.Contains(envelope.Disposition) || envelope.Confidence is < 0 or > 1)
        {
            error = "Goal analysis fields exceed the bounded contract.";
            return false;
        }
        if (!ValidatePlan(envelope.WorkPlan, out error)) return false;
        if (!string.IsNullOrWhiteSpace(validationContextJson))
        {
            try
            {
                var context = JsonSerializer.Deserialize<AiAssistantGoalPlanningValidationContextDto>(validationContextJson, JsonOptions);
                if (context == null)
                {
                    error = "Goal planning validation context is invalid.";
                    return false;
                }
            }
            catch (JsonException)
            {
                error = "Goal planning validation context is invalid JSON.";
                return false;
            }
        }
        return true;
    }

    public static bool TryBuildResult(
        string content,
        string validationContextJson,
        string provider,
        string model,
        out AiAssistantGoalPlanningResultDto? result,
        out string? error)
    {
        result = null;
        if (!TryValidateModel(content, validationContextJson, out error) ||
            !TryDeserialize(content, out var envelope, out error) || envelope == null) return false;

        AiAssistantGoalPlanningValidationContextDto? context;
        try
        {
            context = JsonSerializer.Deserialize<AiAssistantGoalPlanningValidationContextDto>(validationContextJson, JsonOptions);
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }
        if (context == null)
        {
            error = "Goal planning validation context is missing.";
            return false;
        }

        // The model may rank a superficially related read skill for a request that actually asks
        // Qaly to execute a capability which is not installed. The server owns this boundary.
        if (TryCreateKnownTestCapabilityPlan(
                context.Message,
                context.ClientContext,
                context.AvailableSkills,
                out var knownTestPlan) && knownTestPlan != null)
        {
            result = knownTestPlan;
            return true;
        }
        if (TryDetectKnownMissingSkill(context.Message, out var controlledMissingSkill) &&
            !context.AvailableSkills.Any(item => item.CapabilityId == controlledMissingSkill.SkillId))
        {
            result = BuildKnownMissingSkillPlan(
                context.Message,
                context.ClientContext,
                controlledMissingSkill,
                provider,
                model,
                usedFallback: false,
                "Server policy vetoed an unrelated model-selected skill; no executor was called.");
            return true;
        }

        var available = context.AvailableSkills.ToDictionary(item => item.CapabilityId, StringComparer.Ordinal);
        var ranked = envelope.RankedSkills
            .OrderByDescending(item => item.Confidence)
            .FirstOrDefault(item => available.ContainsKey(item.SkillId) &&
                (string.IsNullOrWhiteSpace(context.RequestedCapabilityId) ||
                 string.Equals(item.SkillId, context.RequestedCapabilityId, StringComparison.Ordinal)));
        AiAssistantSkillSelectionDto? selected = null;
        if (!string.IsNullOrWhiteSpace(context.RequestedCapabilityId) &&
            available.TryGetValue(context.RequestedCapabilityId, out var explicitlyRequested))
        {
            selected = ToSelection(
                explicitlyRequested,
                ranked?.FitReason ?? "Người dùng tiếp tục capability đã được máy chủ authorize trong luồng hiện tại.",
                ranked?.Confidence ?? 1);
        }
        else if (ranked != null)
        {
            var descriptor = available[ranked.SkillId];
            selected = ToSelection(descriptor, ranked.FitReason, ranked.Confidence);
        }

        // The model may mistake nouns such as "task" or "Project" in a read request for an
        // action request. Never escalate a deterministic read-only intent into a mutation.
        // Explicit requested capabilities and server-recognized actions retain their route.
        var serverInferredCapabilityId = AiAssistantCapabilityIntentClassifier.Infer(context.Message);
        var modelReadRouteWasCorrected = false;
        var modelActionRouteWasCorrected = false;
        if (string.IsNullOrWhiteSpace(context.RequestedCapabilityId) &&
            IsServerOwnedActionCapability(serverInferredCapabilityId) &&
            available.TryGetValue(serverInferredCapabilityId, out var serverActionDescriptor) &&
            !string.Equals(selected?.SkillId, serverInferredCapabilityId, StringComparison.Ordinal))
        {
            selected = ToSelection(
                serverActionDescriptor,
                "Máy chủ nhận diện action target rõ ràng và giữ đúng capability đã đăng ký; model không được đổi Task Assignment thành tư vấn chung hoặc Project Launch.",
                1);
            modelActionRouteWasCorrected = true;
        }
        if (string.IsNullOrWhiteSpace(context.RequestedCapabilityId) &&
            serverInferredCapabilityId is AiAssistantContextContract.GroundedReadCapability or
                AiAssistantContextContract.ResearchPlanCapability &&
            available.TryGetValue(serverInferredCapabilityId, out var serverReadDescriptor) &&
            (selected == null ||
             available.TryGetValue(selected.SkillId, out var selectedDescriptor) &&
             selectedDescriptor.RiskClass.EndsWith("_mutation", StringComparison.Ordinal)))
        {
            selected = ToSelection(
                serverReadDescriptor,
                "Máy chủ giữ yêu cầu đọc/phân tích ở chế độ không ghi dữ liệu; model không được tự nâng thành thao tác mutation.",
                1);
            modelReadRouteWasCorrected = true;
        }

        var knownButDenied = envelope.RankedSkills.FirstOrDefault(item =>
            AiAssistantCapabilityCatalog.TryGet(item.SkillId, out _) && !available.ContainsKey(item.SkillId));
        var missing = envelope.MissingSkills
            .Where(item => !available.ContainsKey(item.SkillId))
            .GroupBy(item => item.SkillId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(8)
            .ToList();
        if (selected == null && knownButDenied != null && missing.All(item => item.SkillId != knownButDenied.SkillId))
        {
            missing.Add(new AiAssistantMissingSkillDto(
                knownButDenied.SkillId,
                "Skill chưa được cấp quyền",
                "Capability tồn tại nhưng không được authorize trong scope hiện tại.",
                "Yêu cầu quyền phù hợp hoặc đổi sang ngữ cảnh được phép."));
        }

        var disposition = selected != null
            ? (string.Equals(selected.SkillId, AiAssistantContextContract.GroundedReadCapability, StringComparison.Ordinal)
                ? "answerable" : "plannable")
            : knownButDenied != null ? "policy_blocked"
            : envelope.Unknowns.Any(item => item.Blocking) ? "clarification_required"
            : "unsupported_but_analyzed";
        var scope = BuildServerScope(context.ClientContext);
        var plan = BuildServerPlan(envelope.Objective, scope, selected, envelope.Unknowns, disposition);
        var warnings = envelope.Warnings
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(10)
            .ToList();
        if (modelReadRouteWasCorrected)
            warnings.Add("Model-selected or unavailable mutation route was replaced by the server-owned read-only route.");
        if (modelActionRouteWasCorrected)
            warnings.Add("Model-selected route was replaced by the server-owned registered action capability.");
        if (selected == null) warnings.Add("Không có skill đã authorize phù hợp; chưa thực hiện mutation hoặc tool call.");

        var analysis = new AiAssistantGoalAnalysisDto(
            AiAssistantGoalPlanningContract.SchemaId,
            AiAssistantGoalPlanningContract.PromptId,
            AiAssistantGoalPlanningContract.PromptVersion,
            envelope.Objective.Trim(),
            envelope.UserJob.Trim(),
            Clean(envelope.IntentFacets, 8),
            [scope],
            Clean(envelope.Constraints, 10),
            envelope.Unknowns.Take(8).ToArray(),
            Clean(envelope.Assumptions, 10),
            selected == null ? [] : [selected],
            missing,
            NormalizeRisk(envelope.RiskLevel),
            selected?.ConfirmationPolicy != "none",
            disposition,
            Math.Clamp(envelope.Confidence, 0, 1),
            warnings,
            string.IsNullOrWhiteSpace(provider) ? "unknown" : provider,
            string.IsNullOrWhiteSpace(model) ? "unknown" : model,
            false);
        result = new AiAssistantGoalPlanningResultDto(analysis, plan, selected?.SkillId, false);
        return true;
    }

    private static bool IsServerOwnedActionCapability(string capabilityId)
        => capabilityId is
            AiAssistantContextContract.TaskCreateCapability or
            AiAssistantContextContract.TaskAssignmentScheduleCapability or
            AiAssistantContextContract.ProjectLaunchCapability or
            AiAssistantContextContract.ProjectStaffingPlanCapability or
            AiAssistantContextContract.ProjectLaunchExecuteCapability or
            AiAssistantContextContract.ProjectOperationMonitorCapability or
            AiAssistantContextContract.SafeTestRunCapability or
            AiAssistantContextContract.AcceptanceChecklistCapability or
            AiAssistantContextContract.TaskBreakdownCapability or
            AiAssistantContextContract.WikiBriefTaskCapability or
            AiAssistantContextContract.GroupPollCapability or
            AiAssistantContextContract.ProjectDigestCapability or
            AiAssistantContextContract.MeetingActionsCapability or
            AiAssistantContextContract.RoadmapAdjustCapability or
            AiAssistantContextContract.SkillEvidenceCapability;

    public static AiAssistantGoalPlanningResultDto CreateDeterministicFallback(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto discoveryContext,
        string reason)
    {
        if (TryCreateKnownMissingSkillPlan(request, discoveryContext, out var controlledPlan, reason) &&
            controlledPlan != null)
            return controlledPlan;

        var message = request.Message.Trim();
        var normalized = Normalize(message);
        var inferredCapabilityId = AiAssistantCapabilityIntentClassifier.Infer(message, request.History);
        var available = discoveryContext.Capabilities.ToDictionary(item => item.CapabilityId, StringComparer.Ordinal);
        var missing = new List<AiAssistantMissingSkillDto>();
        string? selectedId = null;

        if (ContainsAny(normalized, "test demo", "demo tat ca", "chay test", "test cand", "kiem thu tat ca") &&
            available.ContainsKey(AiAssistantContextContract.SafeTestRunCapability))
        {
            selectedId = AiAssistantContextContract.SafeTestRunCapability;
        }
        else if (ContainsAny(normalized, "test demo", "demo tat ca", "chay test", "test cand", "kiem thu tat ca"))
        {
            missing.Add(new AiAssistantMissingSkillDto(
                "demo.test.run.v1", "Chạy bộ test/demo", "Qaly chưa expose test runner như một skill an toàn cho trợ lý.",
                "Thiết kế một adapter test allowlist, sandbox và report read-only riêng."));
        }
        else if (AiAssistantCapabilityIntentClassifier.IsCapabilityOverviewQuery(message) &&
                 available.ContainsKey(AiAssistantContextContract.GroundedReadCapability))
        {
            selectedId = AiAssistantContextContract.GroundedReadCapability;
        }
        else if (inferredCapabilityId == AiAssistantContextContract.TaskCreateCapability &&
                 available.ContainsKey(AiAssistantContextContract.TaskCreateCapability))
        {
            // Do not let a project-purpose clause inside an explicit task request fall through
            // to the broader Project Launch keyword set below.
            selectedId = AiAssistantContextContract.TaskCreateCapability;
        }
        else if (AiNativeDomainActionContract.CapabilityIds.Contains(inferredCapabilityId) &&
                 available.ContainsKey(inferredCapabilityId))
        {
            selectedId = inferredCapabilityId;
        }
        else if ((ContainsAny(
                     normalized,
                     "tao du an", "tao mot du an", "lap du an", "tao project", "tao mot project",
                     "khoi chay du an", "khoi tao du an", "launch project", "tu dong tao project",
                     "tu dong tao", "plan 18", "18_native", "chi dinh manager", "member", "phan bo", "giao viec",
                     "thu nghiem luon", "thu nghiem", "thu luon", "chay luon", "trien khai luon", "bat dau luon") ||
                     inferredCapabilityId == AiAssistantContextContract.ProjectLaunchCapability) &&
                 available.ContainsKey(AiAssistantContextContract.ProjectLaunchCapability))
        {
            selectedId = AiAssistantContextContract.ProjectLaunchCapability;
        }
        else if (ContainsAny(
                     normalized,
                     "tao du an", "tao mot du an", "lap du an", "tao project", "tao mot project",
                     "tao group", "tao mot group", "tao nhom", "tao mot nhom",
                     "tao cuoc hop", "tao mot cuoc hop", "tao meeting", "tao poll", "tao form", "tao lich"))
        {
            missing.Add(new AiAssistantMissingSkillDto(
                "domain.object.draft.v1", "Soạn đối tượng Qaly", "Chưa có draft adapter tương ứng cho loại đối tượng được yêu cầu.",
                "Bổ sung capability-specific schema, review renderer và confirm endpoint."));
        }
        else
        {
            var hinted = request.RequestedCapabilityId ?? inferredCapabilityId;
            if (available.ContainsKey(hinted)) selectedId = hinted;
            else if (AiAssistantCapabilityCatalog.TryGet(hinted, out var denied))
            {
                missing.Add(new AiAssistantMissingSkillDto(
                    denied.CapabilityId, denied.Title, "Skill tồn tại nhưng không được authorize trong ngữ cảnh hiện tại.",
                    "Đổi scope hoặc yêu cầu domain permission phù hợp."));
            }
        }

        AiAssistantSkillSelectionDto? selected = selectedId == null
            ? null
            : ToSelection(available[selectedId], "Deterministic fallback matched a registered skill.", 0.55);
        var disposition = selected != null
            ? selected.SkillId == AiAssistantContextContract.GroundedReadCapability ? "answerable" : "plannable"
            : missing.Any(item => AiAssistantCapabilityCatalog.TryGet(item.SkillId, out _)) ? "policy_blocked"
            : "unsupported_but_analyzed";
        var scope = BuildServerScope(request.Context);
        var warnings = new[]
        {
            "Goal planner AI không khả dụng; hệ thống dùng fallback giới hạn và không giả lập kết quả model.",
            reason
        }.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        var analysis = new AiAssistantGoalAnalysisDto(
            AiAssistantGoalPlanningContract.SchemaId,
            AiAssistantGoalPlanningContract.PromptId,
            AiAssistantGoalPlanningContract.PromptVersion,
            message,
            message,
            [selected == null ? "unsupported_or_missing_skill" : selected.SkillId],
            [scope],
            [], [], [],
            selected == null ? [] : [selected],
            missing,
            selected?.RiskClass == "project_mutation" ? "medium" : "low",
            selected?.ConfirmationPolicy != "none",
            disposition,
            selected == null ? 0.35 : 0.55,
            warnings,
            "not_reached",
            "not_reached",
            true);
        return new AiAssistantGoalPlanningResultDto(
            analysis,
            BuildServerPlan(message, scope, selected, [], disposition),
            selected?.SkillId,
            true);
    }

    public static bool TryCreateKnownMissingSkillPlan(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto discoveryContext,
        out AiAssistantGoalPlanningResultDto? result,
        string? fallbackReason = null)
    {
        result = null;
        if (TryCreateKnownTestCapabilityPlan(
                request.Message,
                request.Context,
                discoveryContext.Capabilities,
                out var availablePlan) && availablePlan != null)
        {
            result = availablePlan;
            return true;
        }
        if (!TryDetectKnownMissingSkill(request.Message, out var missingSkill)) return false;
        if (discoveryContext.HasCapability(missingSkill.SkillId)) return false;

        var isFallback = !string.IsNullOrWhiteSpace(fallbackReason);
        result = BuildKnownMissingSkillPlan(
            request.Message.Trim(),
            request.Context,
            missingSkill,
            isFallback ? "not_reached" : "Qaly policy router",
            isFallback ? "not_reached" : "known-missing-skill-v1",
            isFallback,
            isFallback
                ? $"Goal planner provider was unavailable ({fallbackReason}); server policy still prevented execution."
                : "Known unavailable execution capability was identified before provider routing; no provider or executor was called.");
        return true;
    }

    public static bool TryCreateAuthorizedExecutionPlan(
        AiAssistantTurnRequestDto request,
        AiAssistantExecutionContextDto discoveryContext,
        out AiAssistantGoalPlanningResultDto? result)
    {
        result = null;
        var capabilityId = request.RequestedCapabilityId ??
            AiAssistantCapabilityIntentClassifier.Infer(request.Message, request.History);
        if (capabilityId is not (
                AiAssistantContextContract.TaskCreateCapability or
                AiAssistantContextContract.ProjectLaunchCapability or
                AiAssistantContextContract.ProjectStaffingPlanCapability or
                AiAssistantContextContract.ProjectLaunchExecuteCapability or
                AiAssistantContextContract.ProjectOperationMonitorCapability or
                AiAssistantContextContract.TaskAssignmentScheduleCapability or
                AiAssistantContextContract.AcceptanceChecklistCapability or
                AiAssistantContextContract.TaskBreakdownCapability or
                AiAssistantContextContract.WikiBriefTaskCapability or
                AiAssistantContextContract.GroupPollCapability or
                AiAssistantContextContract.ProjectDigestCapability or
                AiAssistantContextContract.MeetingActionsCapability or
                AiAssistantContextContract.RoadmapAdjustCapability or
                AiAssistantContextContract.SkillEvidenceCapability) ||
            !discoveryContext.Capabilities.Any(item => item.CapabilityId == capabilityId) ||
            !AiAssistantCapabilityCatalog.TryGet(capabilityId, out var descriptor))
            return false;

        var scope = BuildServerScope(request.Context);
        var selected = ToSelection(
            descriptor,
            request.RequestedCapabilityId == capabilityId
                ? "Tiếp tục capability đã được máy chủ authorize trong luồng hiện tại."
                : "Server intent router khớp yêu cầu hành động với capability đã đăng ký.",
            1);
        var objective = request.Message.Trim();
        var analysis = new AiAssistantGoalAnalysisDto(
            AiAssistantGoalPlanningContract.SchemaId,
            AiAssistantGoalPlanningContract.PromptId,
            AiAssistantGoalPlanningContract.PromptVersion,
            objective,
            descriptor.UserJobs is { Count: > 0 } ? descriptor.UserJobs[0] : objective,
            [capabilityId],
            [scope],
            [],
            [],
            [],
            [selected],
            [],
            descriptor.RiskClass.EndsWith("_mutation", StringComparison.Ordinal) ? "medium" : "low",
            descriptor.ConfirmationPolicy != "none",
            "plannable",
            1,
            [],
            "Qaly capability router",
            "authorized-execution-route-v1",
            false);
        result = new AiAssistantGoalPlanningResultDto(
            analysis,
            BuildServerPlan(objective, scope, selected, [], "plannable"),
            capabilityId,
            false);
        return true;
    }

    private static bool TryCreateKnownTestCapabilityPlan(
        string message,
        AiAssistantClientContextDto? context,
        IReadOnlyList<AiAssistantCapabilityDescriptorDto> availableSkills,
        out AiAssistantGoalPlanningResultDto? result)
    {
        result = null;
        if (!TryDetectKnownMissingSkill(message, out _) ||
            !availableSkills.Any(item => item.CapabilityId == AiSafeTestOrchestratorContract.CapabilityId) ||
            !AiAssistantCapabilityCatalog.TryGet(AiSafeTestOrchestratorContract.CapabilityId, out var descriptor))
            return false;

        var scope = BuildServerScope(context);
        var selected = ToSelection(
            descriptor,
            "Yêu cầu test/demo khớp adapter Development/Test có manifest do máy chủ sở hữu.",
            1);
        var analysis = new AiAssistantGoalAnalysisDto(
            AiAssistantGoalPlanningContract.SchemaId,
            AiAssistantGoalPlanningContract.PromptId,
            AiAssistantGoalPlanningContract.PromptVersion,
            message.Trim(),
            "Xem trước và chạy evidence test/demo cho các CAND trong manifest cố định.",
            ["test_execution", "candidate_verification", "fixed_manifest"],
            [scope],
            ["Chỉ Development/Test", "Không nhận command hoặc path từ chat", "Bắt buộc xác nhận trước khi chạy"],
            [],
            [],
            [selected],
            [],
            "low",
            true,
            "plannable",
            1,
            [],
            "Qaly Safe Test Router",
            "fixed-manifest-v1",
            false);
        result = new AiAssistantGoalPlanningResultDto(
            analysis,
            BuildServerPlan(message.Trim(), scope, selected, [], "plannable"),
            selected.SkillId,
            false);
        return true;
    }

    private static AiAssistantGoalPlanningResultDto BuildKnownMissingSkillPlan(
        string message,
        AiAssistantClientContextDto? context,
        AiAssistantMissingSkillDto missingSkill,
        string provider,
        string model,
        bool usedFallback,
        string warning)
    {
        var scope = BuildServerScope(context);
        const string disposition = "unsupported_but_analyzed";
        var analysis = new AiAssistantGoalAnalysisDto(
            AiAssistantGoalPlanningContract.SchemaId,
            AiAssistantGoalPlanningContract.PromptId,
            AiAssistantGoalPlanningContract.PromptVersion,
            message.Trim(),
            "Chạy và xác minh các bản demo AI-native đã được triển khai.",
            ["test_execution", "candidate_verification", "missing_registered_skill"],
            [scope],
            ["Không cho phép shell tùy ý hoặc test runner không giới hạn."],
            [],
            [],
            [],
            [missingSkill],
            "medium",
            false,
            disposition,
            1,
            [warning],
            provider,
            model,
            usedFallback);
        return new AiAssistantGoalPlanningResultDto(
            analysis,
            BuildServerPlan(message, scope, null, [], disposition),
            null,
            usedFallback);
    }

    private static bool TryDetectKnownMissingSkill(string message, out AiAssistantMissingSkillDto missingSkill)
    {
        var normalized = Normalize(message);
        var mentionsCandidate = normalized.Contains("cand", StringComparison.Ordinal) ||
                                normalized.Contains("candidate", StringComparison.Ordinal);
        var requestsTestExecution = ContainsAny(
            normalized,
            "chay test",
            "chay tu dong",
            "test demo",
            "test cac",
            "test tat ca",
            "kiem thu tat ca",
            "run test",
            "run all test");
        if (mentionsCandidate && requestsTestExecution)
        {
            missingSkill = new AiAssistantMissingSkillDto(
                "demo.test.run.v1",
                "Chạy bộ test/demo",
                "Qaly chưa expose test runner như một skill an toàn cho trợ lý.",
                "Thiết kế một adapter test allowlist, sandbox và report read-only riêng.");
            return true;
        }

        missingSkill = null!;
        return false;
    }

    public static AiAssistantWorkPlanDto CompleteWorkPlan(AiAssistantWorkPlanDto plan, string disposition)
        => plan with
        {
            Steps = plan.Steps.Select(step => step with
            {
                State = step.Kind == "call_skill" && disposition is "unsupported_but_analyzed" or "policy_blocked"
                    ? "blocked" : "completed"
            }).ToArray()
        };

    private static AiAssistantWorkPlanDto BuildServerPlan(
        string objective,
        AiAssistantGoalScopeDto scope,
        AiAssistantSkillSelectionDto? selected,
        IReadOnlyList<AiAssistantGoalUnknownDto> unknowns,
        string disposition)
    {
        var steps = new List<AiAssistantWorkPlanStepDto>
        {
            new("S1", "analyze", "Hiểu mục tiêu và khoanh vùng ngữ cảnh", null, [], [],
                AiAssistantGoalPlanningContract.SchemaId, ["goal_contract_valid"], "none", "completed")
        };
        if (selected != null)
        {
            steps.Add(new("S2", "call_skill", $"Giao cho skill: {selected.Title}", selected.SkillId, [], ["S1"],
                AiAssistantCapabilityCatalog.TryGet(selected.SkillId, out var descriptor) ? descriptor.OutputSchemaId : null,
                ["skill_authorized", "output_schema_valid"],
                selected.RiskClass == "project_mutation" ? "draft_only" : "none", "planned"));
            steps.Add(new("S3", "verify", "Kiểm tra schema, nguồn và policy", selected.SkillId, [], ["S2"], null,
                ["source_grounded", "permission_preserved"], "none", "planned"));
            steps.Add(new("S4", "present", "Trình bày kết quả và lựa chọn tiếp theo", null, [], ["S3"], null,
                ["honest_status"], "none", "planned"));
        }
        else
        {
            steps.Add(new("S2", "present", disposition == "policy_blocked"
                    ? "Giải thích giới hạn quyền và cách tiếp tục an toàn"
                    : "Giải thích skill còn thiếu và đề xuất hướng tiếp tục",
                null, [], ["S1"], null, ["no_unsupported_execution"], "none", "planned"));
        }
        return new AiAssistantWorkPlanDto(
            AiAssistantGoalPlanningContract.WorkPlanSchemaId,
            objective.Trim(), scope,
            selected == null ? [] : [selected.SkillId],
            steps, unknowns.Where(item => item.Blocking).Take(8).ToArray(),
            AiAssistantGoalPlanningContract.MaxSteps,
            AiAssistantGoalPlanningContract.MaxAttemptsPerStep,
            ["policy_denied", "schema_invalid_after_repair", "provider_unavailable", "user_cancelled"],
            selected?.ConfirmationPolicy != "none");
    }

    private static bool ValidatePlan(AiAssistantWorkPlanDto plan, out string? error)
    {
        error = null;
        if (plan == null)
        {
            error = "Work plan is missing.";
            return false;
        }
        if (!string.Equals(plan.SchemaId, AiAssistantGoalPlanningContract.WorkPlanSchemaId, StringComparison.Ordinal) ||
            plan.Steps.Count > AiAssistantGoalPlanningContract.MaxSteps ||
            plan.SelectedSkillIds.Count > 1)
        {
            error = "Work plan bounds or schema identity are invalid.";
            return false;
        }
        var ids = plan.Steps.Select(item => item.StepId).ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length ||
            plan.Steps.Any(step => step.DependencyIds.Any(id => !ids.Contains(id, StringComparer.Ordinal))))
        {
            error = "Work plan step identity or dependencies are invalid.";
            return false;
        }
        var allowedKinds = new HashSet<string>(["analyze", "clarify", "call_skill", "verify", "present"], StringComparer.Ordinal);
        var allowedStates = new HashSet<string>(["planned", "running", "completed", "blocked", "failed"], StringComparer.Ordinal);
        var allowedMutations = new HashSet<string>(["none", "draft_only"], StringComparer.Ordinal);
        if (plan.Steps.Any(step => !allowedKinds.Contains(step.Kind) || !allowedStates.Contains(step.State) ||
                                   !allowedMutations.Contains(step.MutationClass)))
        {
            error = "Work plan contains an unsupported step kind, state or mutation class.";
            return false;
        }
        var dependencies = plan.Steps.ToDictionary(step => step.StepId, step => step.DependencyIds, StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        bool HasCycle(string id)
        {
            if (!visiting.Add(id)) return true;
            if (visited.Contains(id)) { visiting.Remove(id); return false; }
            foreach (var dependency in dependencies[id])
                if (HasCycle(dependency)) return true;
            visiting.Remove(id);
            visited.Add(id);
            return false;
        }
        if (ids.Any(HasCycle))
        {
            error = "Work plan dependencies contain a cycle.";
            return false;
        }
        return true;
    }

    private static bool TryDeserialize(string content, out AiAssistantGoalPlanningEnvelopeDto? envelope, out string? error)
    {
        envelope = null;
        error = null;
        try { envelope = JsonSerializer.Deserialize<AiAssistantGoalPlanningEnvelopeDto>(content, JsonOptions); }
        catch (JsonException exception) { error = exception.Message; return false; }
        if (envelope == null) { error = "Goal analysis response is empty."; return false; }
        return true;
    }

    private static AiAssistantSkillSelectionDto ToSelection(AiAssistantCapabilityDescriptorDto descriptor, string reason, double confidence)
        => new(descriptor.CapabilityId, descriptor.Version, descriptor.Title, reason, Math.Clamp(confidence, 0, 1),
            descriptor.RiskClass, descriptor.ConfirmationPolicy, descriptor.RendererId);

    private static AiAssistantGoalScopeDto BuildServerScope(AiAssistantClientContextDto? context)
    {
        var type = context?.EntityId.HasValue == true && !string.IsNullOrWhiteSpace(context.EntityType)
            ? context.EntityType!.Trim().ToLowerInvariant()
            : context?.ProjectId.HasValue == true ? "project" : "workspace";
        return new AiAssistantGoalScopeDto(type, context?.ProjectId,
            context?.EntityType, context?.EntityId,
            type == "workspace" ? "Không gian làm việc được phép" : $"Ngữ cảnh {type} được phép",
            1, "Scope được server lấy từ client context đã kiểm quyền; không tin ID do model tạo.");
    }

    private static string[] Clean(IEnumerable<string> values, int max)
        => values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal).Take(max).ToArray();
    private static string NormalizeRisk(string value)
        => value is "low" or "medium" or "high" or "critical" ? value : "medium";
    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(term => value.Contains(term, StringComparison.Ordinal));
    private static string Normalize(string value)
    {
        var source = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(source.Length);
        foreach (var character in source)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) builder.Append(character);
        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('đ', 'd');
    }
}
