using System.Globalization;
using System.Text;

namespace Qaly.Application.DTOs.Ai;

public static class AiAssistantContextContract
{
    public const string GroundedReadCapability = AiAssistantTurnContract.GroundedReadIntent;
    public const string ResearchPlanCapability = AiAssistantResearchPlanContract.CapabilityId;
    public const string TaskCreateCapability = AiAssistantTurnContract.TaskCreateIntent;
    public const string TaskAssignmentScheduleCapability = AiAssistantTurnContract.TaskAssignmentScheduleIntent;
    public const string ProjectLaunchCapability = AiProjectLaunchContract.CapabilityId;
    public const string ProjectStaffingPlanCapability = AiProjectOrchestrationContract.StaffingCapabilityId;
    public const string ProjectLaunchExecuteCapability = AiProjectOrchestrationContract.ExecuteCapabilityId;
    public const string ProjectOperationMonitorCapability = AiProjectOrchestrationContract.MonitorCapabilityId;
    public const string SafeTestRunCapability = AiSafeTestOrchestratorContract.CapabilityId;
    public const string AcceptanceChecklistCapability = AiNativeDomainActionContract.ChecklistCapability;
    public const string TaskBreakdownCapability = AiNativeDomainActionContract.BreakdownCapability;
    public const string WikiBriefTaskCapability = AiNativeDomainActionContract.WikiCapability;
    public const string GroupPollCapability = AiNativeDomainActionContract.GroupPollCapability;
    public const string ProjectDigestCapability = AiNativeDomainActionContract.DigestCapability;
    public const string MeetingActionsCapability = AiNativeDomainActionContract.MeetingActionsCapability;
    public const string RoadmapAdjustCapability = AiNativeDomainActionContract.RoadmapAdjustCapability;
    public const string SkillEvidenceCapability = AiNativeDomainActionContract.SkillEvidenceCapability;

    public const string WorkspaceProjectsSource = "workspace.projects";
    public const string ProjectSummarySource = "project.summary";
    public const string ProjectTasksSource = "project.tasks";
    public const string ProjectWorkloadSource = "project.workload";
    public const string ProjectMembersSource = "project.members";
    public const string ProjectSkillsSource = "project.skills";
    public const string TaskDetailSource = "task.detail";
    public const string OrganizationSummarySource = "organization.summary";
    public const string OrganizationRulebookSource = "organization.rulebook";
    public const string WikiPageSource = "wiki.page";
    public const string GroupContextSource = "group.context";

    public static readonly IReadOnlySet<string> KnownSourceIds = new HashSet<string>(StringComparer.Ordinal)
    {
        WorkspaceProjectsSource,
        ProjectSummarySource,
        ProjectTasksSource,
        ProjectWorkloadSource,
        ProjectMembersSource,
        ProjectSkillsSource,
        TaskDetailSource,
        OrganizationSummarySource,
        OrganizationRulebookSource,
        WikiPageSource,
        GroupContextSource
    };
}

public sealed record AiAssistantCapabilityDescriptorDto(
    string CapabilityId,
    string Kind,
    string InputSchemaId,
    string OutputSchemaId,
    IReadOnlyList<string> RequiredScopes,
    IReadOnlyList<string> ContextSources,
    string RiskClass,
    string ConfirmationPolicy,
    string ModelProfile,
    string RendererId,
    string FeatureFlag,
    string Version = "1.0.0",
    string Title = "",
    string Description = "",
    IReadOnlyList<string>? UserJobs = null,
    IReadOnlyList<string>? EntityTypes = null,
    string Executor = "canonical_ai_gateway",
    string VerificationPolicy = "schema_and_source_validation",
    string RollbackPolicy = "feature_disable",
    string Owner = "qaly_ai_platform");

public sealed record AiAssistantContextSourceEnvelopeDto(
    string SourceId,
    string SourceRef,
    string SourceType,
    string Title,
    DateTimeOffset FreshnessAt,
    string TrustClass,
    string PrivacyClass,
    string ContentHash,
    IReadOnlyDictionary<string, object?> Facts,
    IReadOnlyList<string> Redactions,
    string RetrievalMethod);

public sealed record AiAssistantSourceDisclosureDto(
    string SourceId,
    string Status,
    string Label,
    string? SourceRef = null,
    string? ReasonCode = null);

public sealed record AiAssistantExecutionContextDto(
    IReadOnlyList<AiAssistantCapabilityDescriptorDto> Capabilities,
    IReadOnlyList<AiAssistantContextSourceEnvelopeDto> Sources,
    IReadOnlyList<AiAssistantSourceDisclosureDto> SourceDisclosures)
{
    public bool HasCapability(string capabilityId)
        => Capabilities.Any(item => string.Equals(item.CapabilityId, capabilityId, StringComparison.Ordinal));
}

public static class AiAssistantCapabilityCatalog
{
    private static readonly IReadOnlyDictionary<string, AiAssistantCapabilityDescriptorDto> Descriptors =
        new Dictionary<string, AiAssistantCapabilityDescriptorDto>(StringComparer.Ordinal)
        {
            [AiAssistantContextContract.GroundedReadCapability] = new(
                AiAssistantContextContract.GroundedReadCapability,
                "read",
                "assistant_grounded_read_request.v1",
                "assistant_turn.v1",
                ["project.read"],
                [
                    AiAssistantContextContract.WorkspaceProjectsSource,
                    AiAssistantContextContract.ProjectSummarySource,
                    AiAssistantContextContract.ProjectTasksSource,
                    AiAssistantContextContract.ProjectWorkloadSource,
                    AiAssistantContextContract.TaskDetailSource,
                    AiAssistantContextContract.WikiPageSource,
                    AiAssistantContextContract.GroupContextSource
                ],
                "read_only",
                "none",
                "reasoning_strong",
                "assistant-answer.v1",
                "AiJobsV4:AssistantContextRegistryEnabled",
                Title: "Tra cứu có căn cứ",
                Description: "Trả lời câu hỏi từ dữ liệu Qaly đã kiểm quyền và dẫn lại nguồn.",
                UserJobs: ["explain", "summarize", "inspect"],
                EntityTypes: ["workspace", "project", "task", "wiki", "group"]),
            [AiAssistantContextContract.ResearchPlanCapability] = new(
                AiAssistantContextContract.ResearchPlanCapability,
                "artifact",
                AiAssistantResearchPlanContract.RequestSchemaId,
                AiAssistantResearchPlanContract.SchemaId,
                ["project.read"],
                [
                    AiAssistantContextContract.WorkspaceProjectsSource,
                    AiAssistantContextContract.ProjectSummarySource,
                    AiAssistantContextContract.ProjectTasksSource,
                    AiAssistantContextContract.ProjectWorkloadSource,
                    AiAssistantContextContract.TaskDetailSource,
                    AiAssistantContextContract.WikiPageSource,
                    AiAssistantContextContract.GroupContextSource
                ],
                "read_only_proposal",
                "explicit_adapter_handoff",
                "reasoning_strong",
                AiAssistantResearchPlanContract.RendererId,
                "AiJobsV4:AssistantResearchPlanEnabled",
                Title: "Phân tích và lập phương án",
                Description: "Tổng hợp facts, unknowns, options và action graph có nguồn để hỗ trợ quyết định.",
                UserJobs: ["analyze", "compare", "recommend", "plan"],
                EntityTypes: ["workspace", "project", "task", "wiki", "group"]),
            [AiAssistantContextContract.TaskCreateCapability] = new(
                AiAssistantContextContract.TaskCreateCapability,
                "mutation_draft",
                "ai_action_compose_request.v1",
                "ai_action_intent_envelope.v1",
                ["project.read", "task.create"],
                [
                    AiAssistantContextContract.ProjectSummarySource,
                    AiAssistantContextContract.ProjectMembersSource,
                    AiAssistantContextContract.ProjectSkillsSource,
                    AiAssistantContextContract.ProjectWorkloadSource
                ],
                "project_mutation",
                "explicit_selective_confirm",
                "reasoning_strong",
                "task-plan-review.v1",
                "AiJobsV4:ActionComposerTaskCreateEnabled",
                Title: "Soạn bản nháp task",
                Description: "Soạn task có cấu trúc để người dùng chỉnh sửa và xác nhận chọn lọc.",
                UserJobs: ["create_task", "break_down_work"],
                EntityTypes: ["project"],
                VerificationPolicy: "schema_source_permission_and_human_confirmation"),
            [AiAssistantContextContract.TaskAssignmentScheduleCapability] = new(
                AiAssistantContextContract.TaskAssignmentScheduleCapability,
                "mutation_draft",
                "portfolio_schedule_proposal_request.v1",
                "assignment_schedule_proposal.v1",
                ["project.read", "task.update"],
                [
                    AiAssistantContextContract.TaskDetailSource,
                    AiAssistantContextContract.ProjectTasksSource,
                    AiAssistantContextContract.ProjectWorkloadSource,
                    AiAssistantContextContract.ProjectMembersSource,
                    AiAssistantContextContract.ProjectSkillsSource
                ],
                "project_mutation",
                "explicit_selective_confirm",
                "deterministic_local",
                "assignment-schedule-review.v1",
                "AiJobsV4:ActionComposerEnabled",
                Title: "Phân công task theo capacity và lịch",
                Description: "Mở phương án có thể chỉnh sửa, đối soát tải đa dự án, skill evidence và lịch trước một xác nhận ghi thật.",
                UserJobs: ["assign_task", "rebalance_work", "schedule_task"],
                EntityTypes: ["project", "task"],
                Executor: "portfolio_schedule_executor",
                VerificationPolicy: "permission_source_revision_capacity_availability_idempotency_and_readback"),
            [AiAssistantContextContract.ProjectLaunchCapability] = new(
                AiAssistantContextContract.ProjectLaunchCapability,
                "artifact",
                AiProjectLaunchContract.RequestSchemaId,
                AiProjectLaunchContract.BriefSchemaId,
                ["organization.read"],
                [
                    AiAssistantContextContract.OrganizationSummarySource,
                    AiAssistantContextContract.OrganizationRulebookSource,
                    AiAssistantContextContract.WorkspaceProjectsSource
                ],
                "read_only_proposal",
                "none",
                "reasoning_strong",
                AiProjectLaunchContract.RendererId,
                "AiJobsV4:ProjectLaunchBriefEnabled",
                Title: "Phân tích khởi chạy dự án",
                Description: "Tạo Project Launch Brief và quyết định Rulebook để xem lại; không tạo Project hoặc phân công nhân sự.",
                UserJobs: ["launch_project", "clarify_project", "review_rulebook"],
                EntityTypes: ["workspace", "organization"],
                VerificationPolicy: "schema_source_rulebook_and_zero_domain_mutation"),
            [AiAssistantContextContract.ProjectStaffingPlanCapability] = new(
                AiAssistantContextContract.ProjectStaffingPlanCapability,
                "artifact",
                AiProjectOrchestrationContract.PlanningRequestSchemaId,
                AiProjectOrchestrationContract.PlanSchemaId,
                ["organization.manage"],
                [
                    AiAssistantContextContract.OrganizationSummarySource,
                    AiAssistantContextContract.OrganizationRulebookSource,
                    AiAssistantContextContract.WorkspaceProjectsSource
                ],
                "read_only_proposal",
                "none",
                "reasoning_strong",
                AiProjectOrchestrationContract.PlanRendererId,
                "AiJobsV4:ProjectLaunchPlanningEnabled",
                Title: "Plan staffing and delivery",
                Description: "Creates deterministic staffing scenarios and a reviewable delivery plan from a Launch Brief; no Project is created.",
                UserJobs: ["staff_project", "plan_delivery", "check_capacity"],
                EntityTypes: ["workspace", "organization"],
                VerificationPolicy: "strict_model_schema_plus_deterministic_capacity_skill_rulebook_validation"),
            [AiAssistantContextContract.ProjectLaunchExecuteCapability] = new(
                AiAssistantContextContract.ProjectLaunchExecuteCapability,
                "mutation_draft",
                AiProjectOrchestrationContract.ConfirmRequestSchemaId,
                AiProjectOrchestrationContract.ExecutionReceiptSchemaId,
                ["organization.manage", "project.create", "task.create"],
                [
                    AiAssistantContextContract.OrganizationSummarySource,
                    AiAssistantContextContract.OrganizationRulebookSource,
                    AiAssistantContextContract.WorkspaceProjectsSource
                ],
                "project_mutation",
                "explicit_batch_confirm",
                "reasoning_strong",
                AiProjectOrchestrationContract.PlanRendererId,
                "AiJobsV4:ProjectLaunchExecutionEnabled",
                Title: "Execute reviewed Project launch",
                Description: "Executes only a reviewed scenario with stale-source checks, one internal transaction, idempotency and read-back receipt.",
                UserJobs: ["confirm_project_launch", "create_project_from_plan"],
                EntityTypes: ["workspace", "organization"],
                VerificationPolicy: "permission_rulebook_source_idempotency_transaction_and_readback",
                RollbackPolicy: "impact_checked_soft_delete"),
            [AiAssistantContextContract.ProjectOperationMonitorCapability] = new(
                AiAssistantContextContract.ProjectOperationMonitorCapability,
                "artifact",
                AiProjectOrchestrationContract.MonitorRequestSchemaId,
                AiProjectOrchestrationContract.ReplanSchemaId,
                ["organization.manage", "project.read"],
                [
                    AiAssistantContextContract.ProjectSummarySource,
                    AiAssistantContextContract.ProjectTasksSource,
                    AiAssistantContextContract.ProjectWorkloadSource,
                    AiAssistantContextContract.ProjectMembersSource
                ],
                "read_only_proposal",
                "none",
                "reasoning_strong",
                AiProjectOrchestrationContract.PlanRendererId,
                "AiJobsV4:ProjectOperationMonitoringEnabled",
                Title: "Monitor launch and propose replan",
                Description: "Compares the confirmed baseline with current Project facts and creates a review-only replan proposal.",
                UserJobs: ["monitor_project", "detect_delivery_drift", "propose_replan"],
                EntityTypes: ["project"],
                VerificationPolicy: "deterministic_baseline_current_comparison_and_zero_silent_mutation")
            ,
            [AiAssistantContextContract.SafeTestRunCapability] = new(
                AiAssistantContextContract.SafeTestRunCapability,
                "development_action",
                AiSafeTestOrchestratorContract.PreviewSchemaId,
                AiSafeTestOrchestratorContract.ReportSchemaId,
                ["development.test.execute"],
                [],
                "development_test_execution",
                "explicit_batch_confirm",
                "deterministic_local",
                AiSafeTestOrchestratorContract.RendererId,
                "AiJobsV4:SafeTestOrchestratorEnabled",
                Title: "Chạy acceptance manifest an toàn",
                Description: "Chỉ trong Development/Test: xem trước và chạy manifest kiểm thử cố định, không nhận command từ người dùng.",
                UserJobs: ["run_tests", "verify_candidates", "collect_acceptance_evidence"],
                EntityTypes: ["workspace"],
                Executor: "safe_allowlisted_test_orchestrator",
                VerificationPolicy: "environment_allowlist_confirmation_exit_code_and_durable_report",
                RollbackPolicy: "not_applicable_read_only_test_data"),
            [AiAssistantContextContract.AcceptanceChecklistCapability] = new(
                AiAssistantContextContract.AcceptanceChecklistCapability,
                "mutation_draft",
                AiNativeDomainActionContract.ChecklistSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["project.read", "task.update"],
                [AiAssistantContextContract.TaskDetailSource],
                "project_mutation",
                "explicit_single_confirm",
                "reasoning_strong",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Soạn acceptance checklist",
                Description: "Tạo checklist persisted cho Task sau một lần review và xác nhận.",
                UserJobs: ["create_acceptance_checklist", "define_done"],
                EntityTypes: ["task"],
                Executor: "ai_native_action_executor",
                VerificationPolicy: "target_permission_source_revision_idempotency_transaction_and_readback"),
            [AiAssistantContextContract.TaskBreakdownCapability] = new(
                AiAssistantContextContract.TaskBreakdownCapability,
                "mutation_draft",
                AiNativeDomainActionContract.BreakdownSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["project.read", "task.create"],
                [AiAssistantContextContract.TaskDetailSource, AiAssistantContextContract.ProjectTasksSource],
                "project_mutation",
                "explicit_single_confirm",
                "reasoning_strong",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Tách task cha thành subtask",
                Description: "Tạo parent/subtask canonical, có thứ tự và cùng Project sau review.",
                UserJobs: ["break_down_task", "create_subtasks"],
                EntityTypes: ["task"],
                Executor: "ai_native_action_executor",
                VerificationPolicy: "target_permission_source_revision_exact_count_transaction_and_readback"),
            [AiAssistantContextContract.WikiBriefTaskCapability] = new(
                AiAssistantContextContract.WikiBriefTaskCapability,
                "mutation_draft",
                AiNativeDomainActionContract.WikiSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["project.read", "wiki.read", "task.create"],
                [AiAssistantContextContract.WikiPageSource],
                "project_mutation",
                "explicit_single_confirm",
                "reasoning_strong",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Tóm tắt Wiki và soạn task",
                Description: "Tạo brief có nguồn section-level và Task tùy chọn từ Wiki hiện tại.",
                UserJobs: ["summarize_wiki", "create_task_from_wiki"],
                EntityTypes: ["wiki"],
                Executor: "ai_native_action_executor"),
            [AiAssistantContextContract.GroupPollCapability] = new(
                AiAssistantContextContract.GroupPollCapability,
                "mutation_draft",
                AiNativeDomainActionContract.GroupPollSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["group.read", "group.poll.create"],
                [AiAssistantContextContract.GroupContextSource],
                "group_mutation",
                "explicit_single_confirm",
                "reasoning_strong",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Soạn bình chọn nhóm",
                Description: "Soạn đúng một Poll editable và tạo canonical Poll sau xác nhận.",
                UserJobs: ["create_group_poll"],
                EntityTypes: ["group"],
                Executor: "ai_native_action_executor"),
            [AiAssistantContextContract.ProjectDigestCapability] = new(
                AiAssistantContextContract.ProjectDigestCapability,
                "mutation_draft",
                AiNativeDomainActionContract.DigestSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["project.read", "project.manage"],
                [AiAssistantContextContract.ProjectSummarySource, AiAssistantContextContract.ProjectTasksSource],
                "project_mutation",
                "explicit_single_confirm",
                "deterministic_local",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Cấu hình weekly digest",
                Description: "Lưu preference và lịch gửi Project digest trên máy chủ.",
                UserJobs: ["configure_weekly_digest"],
                EntityTypes: ["project"],
                Executor: "ai_native_action_executor"),
            [AiAssistantContextContract.MeetingActionsCapability] = new(
                AiAssistantContextContract.MeetingActionsCapability,
                "mutation_draft",
                AiNativeDomainActionContract.MeetingActionsSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["project.read", "meeting.read", "task.create"],
                [AiAssistantContextContract.ProjectTasksSource],
                "project_mutation",
                "explicit_selective_confirm",
                "reasoning_strong",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Rà soát action item cuộc họp",
                Description: "Hiển thị quyết định, blocker và action item có nguồn; chỉ map hoặc tạo Task đã chọn sau xác nhận.",
                UserJobs: ["review_meeting", "map_meeting_actions"],
                EntityTypes: ["meeting"],
                Executor: "ai_native_action_executor",
                VerificationPolicy: "meeting_source_permission_selective_confirmation_and_readback"),
            [AiAssistantContextContract.RoadmapAdjustCapability] = new(
                AiAssistantContextContract.RoadmapAdjustCapability,
                "mutation_draft",
                AiNativeDomainActionContract.RoadmapAdjustSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["project.read", "sprint.update"],
                [AiAssistantContextContract.ProjectSummarySource, AiAssistantContextContract.ProjectTasksSource,
                    AiAssistantContextContract.ProjectWorkloadSource],
                "project_mutation",
                "explicit_selective_confirm",
                "deterministic_local",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Điều chỉnh Roadmap/Sprint",
                Description: "Đối chiếu dependency, capacity và deadline; hiển thị before/after trước khi ghi Sprint đã chọn.",
                UserJobs: ["review_roadmap", "adjust_sprint"],
                EntityTypes: ["project"],
                Executor: "ai_native_action_executor",
                VerificationPolicy: "permission_source_revision_selective_confirmation_and_readback"),
            [AiAssistantContextContract.SkillEvidenceCapability] = new(
                AiAssistantContextContract.SkillEvidenceCapability,
                "mutation_draft",
                AiNativeDomainActionContract.SkillEvidenceSchemaId,
                AiNativeDomainActionContract.ReceiptSchemaId,
                ["project.read", "skill_evidence.confirm"],
                [AiAssistantContextContract.TaskDetailSource, AiAssistantContextContract.ProjectSkillsSource],
                "project_mutation",
                "explicit_single_confirm",
                "deterministic_local",
                AiNativeDomainActionContract.RendererId,
                "AiJobsV4:NativeDomainActionsEnabled",
                Title: "Xác nhận đóng góp và bằng chứng kỹ năng",
                Description: "Chỉ dùng Task Done, assignee, required skill và acceptance checklist đã xác nhận; không dùng label/chat riêng.",
                UserJobs: ["confirm_skill_evidence", "attribute_completion"],
                EntityTypes: ["task"],
                Executor: "ai_native_action_executor",
                VerificationPolicy: "done_task_confirmed_acceptance_assignee_skill_permission_and_readback")
        };

    private static readonly HashSet<string> KnownSchemaIds = new(StringComparer.Ordinal)
    {
        "assistant_grounded_read_request.v1",
        "assistant_turn.v1",
        AiAssistantResearchPlanContract.RequestSchemaId,
        AiAssistantResearchPlanContract.SchemaId,
        "ai_action_compose_request.v1",
        "ai_action_intent_envelope.v1",
        "portfolio_schedule_proposal_request.v1",
        "assignment_schedule_proposal.v1",
        AiProjectLaunchContract.RequestSchemaId,
        AiProjectLaunchContract.BriefSchemaId,
        AiProjectOrchestrationContract.PlanningRequestSchemaId,
        AiProjectOrchestrationContract.PlanSchemaId,
        AiProjectOrchestrationContract.ConfirmRequestSchemaId,
        AiProjectOrchestrationContract.ExecutionReceiptSchemaId,
        AiProjectOrchestrationContract.MonitorRequestSchemaId,
        AiProjectOrchestrationContract.ReplanSchemaId,
        AiSafeTestOrchestratorContract.PreviewSchemaId,
        AiSafeTestOrchestratorContract.ReportSchemaId,
        AiNativeDomainActionContract.ChecklistSchemaId,
        AiNativeDomainActionContract.BreakdownSchemaId,
        AiNativeDomainActionContract.WikiSchemaId,
        AiNativeDomainActionContract.GroupPollSchemaId,
        AiNativeDomainActionContract.DigestSchemaId,
        AiNativeDomainActionContract.MeetingActionsSchemaId,
        AiNativeDomainActionContract.RoadmapAdjustSchemaId,
        AiNativeDomainActionContract.SkillEvidenceSchemaId,
        AiNativeDomainActionContract.ReceiptSchemaId
    };

    private static readonly HashSet<string> KnownRendererIds = new(StringComparer.Ordinal)
    {
        "assistant-answer.v1",
        AiAssistantResearchPlanContract.RendererId,
        "task-plan-review.v1",
        "assignment-schedule-review.v1",
        AiProjectLaunchContract.RendererId,
        AiProjectOrchestrationContract.PlanRendererId,
        AiSafeTestOrchestratorContract.RendererId,
        AiNativeDomainActionContract.RendererId
    };

    static AiAssistantCapabilityCatalog()
    {
        foreach (var descriptor in Descriptors.Values)
        {
            if (!KnownSchemaIds.Contains(descriptor.InputSchemaId) ||
                !KnownSchemaIds.Contains(descriptor.OutputSchemaId) ||
                !KnownRendererIds.Contains(descriptor.RendererId) ||
                descriptor.ContextSources.Any(sourceId => !AiAssistantContextContract.KnownSourceIds.Contains(sourceId)))
            {
                throw new InvalidOperationException($"Capability descriptor '{descriptor.CapabilityId}' references an unknown contract.");
            }
        }
    }

    public static IReadOnlyCollection<AiAssistantCapabilityDescriptorDto> All { get; } = Descriptors.Values.ToArray();

    public static bool TryGet(string capabilityId, out AiAssistantCapabilityDescriptorDto descriptor)
        => Descriptors.TryGetValue(capabilityId, out descriptor!);
}

public static class AiAssistantCapabilityIntentClassifier
{
    public static bool IsExternalAdapterStatusQuery(string? message)
    {
        var normalized = Normalize(message ?? string.Empty);
        var externalTargets = new[] { "calendar", "repository", "invitation", "webhook", "deployment" };
        var mentionedTargets = externalTargets.Count(target => normalized.Contains(target, StringComparison.Ordinal));
        return mentionedTargets >= 2 && ContainsAny(normalized,
            "adapter", "dong bo", "read back", "read-back", "external deferred", "external_deferred",
            "kiem tra kha nang", "kiem tra tich hop");
    }

    public static bool IsCapabilityOverviewQuery(string? message)
    {
        var normalized = Normalize(message ?? string.Empty);
        var matchesKnownPhrase = ContainsAny(
            normalized,
            "giup toi tu dong nhung gi",
            "tu dong nhung gi",
            "tu dong duoc gi",
            "kha nang ai",
            "capability ai native",
            "capability hien tai",
            "capabilities",
            "quyen hien tai cua toi",
            "role hien tai cua toi",
            "lam duoc gi",
            "giup toi nhung gi",
            "toi co the hoi gi",
            "nen hoi gi",
            "bat dau tu dau",
            "huong dan su dung tro ly",
            "tro ly ho tro gi");
        var asksWhatHelpIsAvailable =
            ContainsAny(normalized, "giup toi", "giup cho toi", "ho tro toi", "ho tro cho toi") &&
            ContainsAny(normalized, "nhung gi", "lam gi", "duoc gi", "ho tro gi");
        return matchesKnownPhrase || asksWhatHelpIsAvailable;
    }

    public static string Infer(string message, IEnumerable<AiChatMessageDto>? history)
    {
        var direct = Infer(message);
        var contextualTaskFollowUp = direct == AiAssistantContextContract.TaskCreateCapability &&
            IsContextReference(message);
        if ((direct != AiAssistantContextContract.GroundedReadCapability && !contextualTaskFollowUp) ||
            (!IsContinuation(message) && !contextualTaskFollowUp))
            return direct;

        var recentContext = string.Join(' ', (history ?? [])
            .TakeLast(6)
            .Select(item => item.Content ?? string.Empty));
        var normalizedContext = Normalize(recentContext);
        if (ContainsAny(normalizedContext,
                "project launch", "launch brief", "tao project", "tao du an", "khoi chay du an",
                "staffing", "chi dinh manager", "phan bo thanh vien", "tu dong lap project"))
            return AiAssistantContextContract.ProjectLaunchCapability;
        if (ContainsAny(normalizedContext, "acceptance checklist", "checklist nghiem thu"))
            return AiAssistantContextContract.AcceptanceChecklistCapability;
        if (ContainsAny(normalizedContext, "subtask", "tach task", "breakdown task"))
            return AiAssistantContextContract.TaskBreakdownCapability;
        if (ContainsAny(normalizedContext, "wiki", "tai lieu"))
            return AiAssistantContextContract.WikiBriefTaskCapability;
        if (ContainsAny(normalizedContext, "poll", "binh chon"))
            return AiAssistantContextContract.GroupPollCapability;
        if (ContainsAny(normalizedContext, "weekly digest", "bao cao hang tuan"))
            return AiAssistantContextContract.ProjectDigestCapability;
        if (ContainsAny(normalizedContext, "transcript", "action item cuoc hop", "bien ban cuoc hop"))
            return AiAssistantContextContract.MeetingActionsCapability;
        if (ContainsAny(normalizedContext, "roadmap", "dieu chinh sprint"))
            return AiAssistantContextContract.RoadmapAdjustCapability;
        if (ContainsAny(normalizedContext, "skill evidence", "bang chung ky nang", "attribution"))
            return AiAssistantContextContract.SkillEvidenceCapability;
        if (ContainsAny(normalizedContext,
                "giao task", "giao viec", "phan cong", "tu giao", "assignment schedule",
                "doi chieu required skill", "capacity that", "availability"))
            return AiAssistantContextContract.TaskAssignmentScheduleCapability;
        if (ContainsAny(normalizedContext, "tao task", "tao nhiem vu", "soan task"))
            return AiAssistantContextContract.TaskCreateCapability;
        return direct;
    }

    public static string Infer(string message)
    {
        var normalized = Normalize(message ?? string.Empty);
        if (IsExternalAdapterStatusQuery(message))
            return AiAssistantContextContract.GroundedReadCapability;
        var hasProjectNoun = ContainsAny(normalized, "du an", "project", "web spa", "san pham moi", "plan 18", "18_native", "chuc nang ai native");
        var asksForCandidateTests = ContainsAny(normalized, "chay test", "run test", "kiem thu", "test demo") &&
            ContainsAny(normalized, "cand", "candidate", "ai native");
        if (asksForCandidateTests)
            return AiAssistantContextContract.SafeTestRunCapability;
        if (hasProjectNoun && ContainsAny(normalized,
                "monitor", "theo doi", "replan", "lap lai ke hoach", "lech tien do",
                "ket qua thuc thi", "execution receipt", "idempotency", "retry cung yeu cau", "tao trung project"))
            return AiAssistantContextContract.ProjectOperationMonitorCapability;
        if (hasProjectNoun &&
            ContainsAny(normalized, "phuong an dang chon", "phuong an da chon", "selected scenario") &&
            ContainsAny(normalized, "card review", "mot xac nhan", "truoc khi ghi", "xac nhan tao project"))
            return AiAssistantContextContract.ProjectLaunchExecuteCapability;
        if (hasProjectNoun && ContainsAny(normalized, "xac nhan khoi chay", "thuc thi launch", "execute launch", "tao project tu plan"))
            return AiAssistantContextContract.ProjectLaunchExecuteCapability;
        if (IsCapabilityOverviewQuery(message))
            return AiAssistantContextContract.GroundedReadCapability;

        var explicitlyReadOnlyOutcome =
            ContainsAny(normalized, "chi xem", "khong thay doi du lieu", "khong ghi du lieu", "khong hien nut xac nhan mutation") ||
            (ContainsAny(normalized, "neu toi khong co quyen", "khong co quyen") &&
             ContainsAny(normalized, "van tra phan tich", "van tra loi", "van tom tat", "nut mo du lieu nguon"));
        if (explicitlyReadOnlyOutcome &&
            ContainsAny(normalized, "tom tat", "phan tich", "liet ke", "du lieu nguon", "tai lieu"))
            return AiAssistantContextContract.GroundedReadCapability;

        if (ContainsAny(normalized, "acceptance checklist", "checklist nghiem thu", "tieu chi nghiem thu") &&
            ContainsAny(normalized, "tao", "soan", "them", "lap"))
            return AiAssistantContextContract.AcceptanceChecklistCapability;
        if (ContainsAny(normalized, "subtask", "task con", "breakdown task", "tach task", "chia task") &&
            ContainsAny(normalized, "tao", "soan", "them", "tach", "chia", "lap"))
            return AiAssistantContextContract.TaskBreakdownCapability;
        var hasExplicitWikiTarget = ContainsAny(normalized, "wiki", "tai lieu nay", "tai lieu tren",
            "tu tai lieu", "theo tai lieu", "dua tren tai lieu", "tom tat tai lieu");
        if (hasExplicitWikiTarget &&
            (ContainsAny(normalized, "tao task", "tao cac task", "soan task", "them task") ||
             ContainsAny(normalized, "de xuat task", "task tuy chon", "tick chon")))
            return AiAssistantContextContract.WikiBriefTaskCapability;
        if (hasExplicitWikiTarget && ContainsAny(normalized, "tom tat", "brief", "giai thich", "phan tich"))
            return AiAssistantContextContract.GroundedReadCapability;
        if (ContainsAny(normalized, "poll", "binh chon") &&
            ContainsAny(normalized, "tao", "soan", "them", "lap"))
            return AiAssistantContextContract.GroupPollCapability;
        if (ContainsAny(normalized, "weekly digest", "bao cao hang tuan", "tong hop hang tuan") &&
            ContainsAny(normalized, "bat", "cau hinh", "tao", "dang ky", "gui", "tat", "dung", "ngung", "huy", "enable", "disable"))
            return AiAssistantContextContract.ProjectDigestCapability;
        if (ContainsAny(normalized, "transcript", "cuoc hop", "meeting") &&
            ContainsAny(normalized, "quyet dinh", "blocker", "action item", "map", "trich"))
            return AiAssistantContextContract.MeetingActionsCapability;
        if (ContainsAny(normalized, "roadmap", "lo trinh", "sprint") &&
            ContainsAny(normalized, "dieu chinh", "before after", "before/after", "cap nhat roadmap", "doi sprint"))
            return AiAssistantContextContract.RoadmapAdjustCapability;
        if (ContainsAny(normalized, "skill evidence", "bang chung ky nang", "attribution", "ghi nhan dong gop") &&
            (ContainsAny(normalized, "attribution", "ghi nhan dong gop") ||
             ContainsAny(normalized, "task vua hoan tat", "task hoan tat", "task da hoan tat")))
            return AiAssistantContextContract.SkillEvidenceCapability;

        // Prefer the action target the user names first. A task request often explains that
        // the tasks are "de khoi tao du an"; treating that purpose clause as the primary
        // action incorrectly opens Project Launch instead of Task Composer. Conversely,
        // "khoi tao du an ... sau do tao task" is still a Project Launch request. Resolve
        // this before staffing keywords because a launch request legitimately mentions team.
        var taskActionIndex = FirstActionObjectIndex(
            normalized,
            ["tao", "khoi tao", "them", "soan", "tach", "lap", "len"],
            "task", "cong viec", "nhiem vu");
        var projectLaunchActionIndex = FirstActionObjectIndex(
            normalized,
            ["tao", "khoi tao", "khoi chay", "bat dau", "lap", "launch", "start"],
            "du an", "project");
        if (taskActionIndex >= 0 || projectLaunchActionIndex >= 0)
        {
            if (taskActionIndex >= 0 &&
                (projectLaunchActionIndex < 0 || taskActionIndex < projectLaunchActionIndex))
                return AiAssistantContextContract.TaskCreateCapability;
            if (projectLaunchActionIndex >= 0)
                return AiAssistantContextContract.ProjectLaunchCapability;
        }

        var asksForStaffingPlan = ContainsAny(normalized,
                "staffing", "phan bo nhan su", "xep nhan su", "kiem tra capacity", "capacity da khai bao",
                "lap delivery plan", "phuong an manager", "manager/team", "chi dinh manager", "chi dinh member",
                "phan chia thanh vien", "muc do phu hop", "lich hop ly", "tai da du an") ||
            (ContainsAny(normalized, "manager", "team") &&
             ContainsAny(normalized, "skill evidence", "capacity", "lich vang", "sprint", "required skill"));
        if (asksForStaffingPlan)
            return AiAssistantContextContract.ProjectStaffingPlanCapability;

        if (ContainsAny(normalized, "giu phuong an hien tai", "giu phuong an", "assignment final confirm") &&
            ContainsAny(normalized, "card xac nhan", "xac nhan cuoi", "khong ghi truoc", "final confirm"))
            return AiAssistantContextContract.TaskAssignmentScheduleCapability;

        var hasTaskNoun = ContainsAny(normalized, "task", "cong viec", "nhiem vu");
        var hasAssignmentVerb = ContainsAny(normalized,
            "assign", "phan cong", "gan cho", "giao cho", "giao task", "giao viec", "tu giao",
            "can bang tai", "phuong an giao viec", "lap lich giao viec");
        if (hasTaskNoun && hasAssignmentVerb)
            return AiAssistantContextContract.TaskAssignmentScheduleCapability;

        var hasLaunchVerb = ContainsAny(normalized, "khoi chay", "khoi tao", "bat dau", "launch", "lap du an", "tao du an", "tu dong tao", "bien chat thanh", "tu dong hoa", "thu nghiem luon", "thu nghiem", "thu luon", "chay luon", "trien khai luon");
        if (hasProjectNoun || hasLaunchVerb || ContainsAny(normalized, "tao project", "manager", "member", "phan tao task"))
        {
            if (hasLaunchVerb || ContainsAny(normalized, "tao project", "plan 18", "18_native"))
                return AiAssistantContextContract.ProjectLaunchCapability;
        }
        var hasCreateVerb = ContainsAny(normalized, "tao", "them", "soan", "tach") ||
            ContainsAny(normalized, "lap task", "lap cong viec", "lap nhiem vu");
        if (hasTaskNoun && hasCreateVerb)
        {
            return AiAssistantContextContract.TaskCreateCapability;
        }

        var hasResearchVerb = ContainsAny(
            normalized,
            "nghien cuu",
            "danh gia",
            "phan tich",
            "tim gap",
            "tim van de",
            "root cause",
            "so sanh phuong an");
        var hasPlanningOutcome = ContainsAny(
            normalized,
            "de xuat",
            "phuong an",
            "khuyen nghi",
            "ke hoach",
            "lo trinh",
            "action plan",
            "trade-off",
            "tradeoff");
        return hasResearchVerb && hasPlanningOutcome
            ? AiAssistantContextContract.ResearchPlanCapability
            : AiAssistantContextContract.GroundedReadCapability;
    }

    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static int FirstPhraseIndex(string value, params string[] phrases)
    {
        var first = -1;
        foreach (var phrase in phrases)
        {
            var index = value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && (first < 0 || index < first)) first = index;
        }

        return first;
    }

    private static int FirstActionObjectIndex(
        string value,
        IReadOnlyList<string> actionVerbs,
        params string[] objectTerms)
    {
        // Keep the verb close to its object. A wider window makes unrelated customization
        // phrases such as "thêm/bớt người ... sửa Task" look like "thêm Task" and steals
        // the staffing route from the user's actual manager/team request.
        const int maximumActionDistance = 32;
        var first = -1;
        foreach (var objectTerm in objectTerms)
        {
            var searchFrom = 0;
            while (searchFrom < value.Length)
            {
                var objectIndex = value.IndexOf(objectTerm, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (objectIndex < 0) break;

                var prefixStart = Math.Max(0, objectIndex - maximumActionDistance);
                var prefix = value[prefixStart..objectIndex];
                if (actionVerbs.Any(verb => prefix.Contains(verb, StringComparison.OrdinalIgnoreCase)) &&
                    (first < 0 || objectIndex < first))
                {
                    first = objectIndex;
                }

                searchFrom = objectIndex + objectTerm.Length;
            }
        }

        return first;
    }

    private static bool IsContinuation(string message)
    {
        var normalized = Normalize(message);
        return normalized.Length <= 80 && ContainsAny(normalized,
            "thu luon", "thu nghiem luon", "lam luon", "lam di", "bat dau di", "tiep tuc",
            "proceed", "go ahead", "ok", "dong y", "chay luon", "trien khai luon");
    }

    private static bool IsContextReference(string message)
    {
        var normalized = Normalize(message);
        return normalized.Length <= 120 && ContainsAny(normalized,
            "tu do", "theo do", "cai do", "noi tren", "phan tren", "tai lieu nay", "wiki nay");
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('đ', 'd');
    }
}
