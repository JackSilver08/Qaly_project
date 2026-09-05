using System.Text.RegularExpressions;

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
    private static readonly string[] CreateVerbs =
        ["tao", "khoi tao", "them", "soan", "lap", "len", "create", "add", "draft", "compose"];

    private static readonly string[] NegationMarkers =
        ["khong", "dung", "chua", "chua can", "khong can", "khong duoc", "tuyet doi khong",
         "khong xac nhan", "chua xac nhan", "dung xac nhan",
         "khong co quyen", "chua co quyen", "not allowed to", "cannot", "can't", "cant",
         "do not", "don't", "dont", "never", "stop"];

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
        // Suggestions in an assistant answer are not user authorization. Do not skip over
        // the latest user topic to revive an older action. Explicit current requests win.
        var previous = (history ?? []).TakeLast(8).LastOrDefault(item =>
            string.Equals(item.Role, "user", StringComparison.OrdinalIgnoreCase) &&
            !IsContinuation(item.Content ?? string.Empty));
        if (previous == null) return direct;
        if (direct == AiAssistantContextContract.TaskCreateCapability && IsContextReference(message) &&
            ContainsAny(Normalize(previous.Content ?? string.Empty), "wiki", "tai lieu"))
            return AiAssistantContextContract.WikiBriefTaskCapability;
        if (IsContinuation(message)) return Infer(previous.Content ?? string.Empty);
        return direct;
    }

    public static string Infer(string message)
    {
        var normalized = Normalize(message ?? string.Empty);
        if (IsExternalAdapterStatusQuery(message))
            return AiAssistantContextContract.GroundedReadCapability;
        var taskActionIndex = FirstActionObjectIndex(normalized, CreateVerbs, "task", "tasks", "cong viec", "nhiem vu");
        var projectLaunchActionIndex = FirstActionObjectIndex(normalized,
            ["tao", "khoi tao", "khoi chay", "bat dau", "lap", "launch", "start",
             "tu dong tao", "bien chat thanh", "tu dong hoa", "create"],
            "du an", "project", "san pham moi", "web spa");
        var hasProjectNoun = ContainsAny(normalized, "du an", "project", "web spa", "san pham moi");
        if (IsSafeTestExecutionRequest(message))
            return AiAssistantContextContract.SafeTestRunCapability;
        if (hasProjectNoun && (ContainsAny(normalized,
                "ket qua thuc thi", "execution receipt", "retry cung yeu cau", "idempotency") ||
            (taskActionIndex < 0 && projectLaunchActionIndex < 0 && ContainsAny(normalized,
                "monitor", "theo doi", "replan", "lap lai ke hoach", "lech tien do",
                "tao trung project"))))
            return AiAssistantContextContract.ProjectOperationMonitorCapability;
        if (hasProjectNoun &&
            ContainsAny(normalized, "phuong an dang chon", "phuong an da chon", "selected scenario") &&
            ContainsAny(normalized, "card review", "mot xac nhan", "truoc khi ghi", "xac nhan tao project") &&
            !IsNegatedAction(normalized, "xac nhan", "execute", "thuc thi"))
            return AiAssistantContextContract.ProjectLaunchExecuteCapability;
        if (hasProjectNoun &&
            ContainsAny(normalized, "xac nhan khoi chay", "thuc thi launch", "execute launch", "tao project tu plan") &&
            !IsNegatedAction(normalized, "xac nhan", "execute", "thuc thi", "tao"))
            return AiAssistantContextContract.ProjectLaunchExecuteCapability;
        if (IsCapabilityOverviewQuery(message))
            return AiAssistantContextContract.GroundedReadCapability;

        var checklistIndex = FirstActionObjectIndex(normalized, CreateVerbs,
            "acceptance checklist", "checklist nghiem thu", "tieu chi nghiem thu");
        var breakdownIndex = FirstActionObjectIndex(normalized, ["tach", "chia", "split", "break down"], "task", "nhiem vu", "cong viec");
        if (breakdownIndex < 0)
            breakdownIndex = FirstActionObjectIndex(normalized, CreateVerbs, "subtask", "subtasks", "task con", "breakdown task");
        var staffingIndex = FirstActionObjectIndex(normalized,
            ["lap", "de xuat", "xay dung", "phan bo", "xep", "can bang", "chi dinh", "chon"],
            "staffing", "nhan su", "manager", "team", "delivery plan");
        var pollIndex = FirstActionObjectIndex(normalized, CreateVerbs, "poll", "binh chon");
        var digestIndex = FirstActionObjectIndex(normalized,
            ["bat", "cau hinh", "tao", "dang ky", "gui", "tat", "ngung", "huy", "enable", "disable"],
            "weekly digest", "bao cao hang tuan", "tong hop hang tuan");
        var firstSpecificCreate = new[] { checklistIndex, breakdownIndex, staffingIndex, pollIndex, digestIndex }
            .Where(index => index >= 0).DefaultIfEmpty(int.MaxValue).Min();
        if (projectLaunchActionIndex >= 0 && projectLaunchActionIndex < firstSpecificCreate &&
            (taskActionIndex < 0 || projectLaunchActionIndex < taskActionIndex))
            return AiAssistantContextContract.ProjectLaunchCapability;
        // A purpose clause must not replace an explicit Task request with a monitor,
        // roadmap adjustment, or other downstream action.
        if (taskActionIndex >= 0 && taskActionIndex < firstSpecificCreate &&
            !ContainsAny(normalized, "wiki", "tai lieu nay", "tai lieu tren", "tu tai lieu", "theo tai lieu", "dua tren tai lieu", "tom tat tai lieu"))
            return AiAssistantContextContract.TaskCreateCapability;
        if (checklistIndex >= 0)
            return AiAssistantContextContract.AcceptanceChecklistCapability;
        if (breakdownIndex >= 0)
            return AiAssistantContextContract.TaskBreakdownCapability;
        if (staffingIndex >= 0 && (taskActionIndex < 0 || staffingIndex < taskActionIndex))
            return AiAssistantContextContract.ProjectStaffingPlanCapability;
        var hasExplicitWikiTarget = ContainsAny(normalized, "wiki", "tai lieu nay", "tai lieu tren",
            "tu tai lieu", "theo tai lieu", "dua tren tai lieu", "tom tat tai lieu");
        if (hasExplicitWikiTarget &&
            (FirstActionObjectIndex(normalized, [.. CreateVerbs, "de xuat"], "task", "cong viec", "nhiem vu") >= 0 ||
             ContainsAny(normalized, "task tuy chon", "tick chon")))
            return AiAssistantContextContract.WikiBriefTaskCapability;
        if (hasExplicitWikiTarget && ContainsAny(normalized, "tom tat", "brief", "giai thich", "phan tich"))
            return AiAssistantContextContract.GroundedReadCapability;
        if (pollIndex >= 0)
            return AiAssistantContextContract.GroupPollCapability;
        if (digestIndex >= 0)
            return AiAssistantContextContract.ProjectDigestCapability;
        if (ContainsAny(normalized, "transcript", "cuoc hop", "meeting") &&
            ContainsAny(normalized, "quyet dinh", "blocker", "action item", "map", "trich") &&
            !IsNegatedAction(normalized, "map", "trich"))
            return AiAssistantContextContract.MeetingActionsCapability;
        if (ContainsAny(normalized, "roadmap", "lo trinh", "sprint") &&
            ContainsAny(normalized, "dieu chinh", "before after", "before/after", "cap nhat roadmap", "doi sprint") &&
            !IsNegatedAction(normalized, "dieu chinh", "cap nhat", "doi"))
            return AiAssistantContextContract.RoadmapAdjustCapability;
        if (ContainsAny(normalized, "skill evidence", "bang chung ky nang", "attribution", "ghi nhan dong gop") &&
            (ContainsAny(normalized, "attribution", "ghi nhan dong gop") ||
             ContainsAny(normalized, "task vua hoan tat", "task hoan tat", "task da hoan tat")) &&
            !IsNegatedAction(normalized, "ghi nhan", "attribution"))
            return AiAssistantContextContract.SkillEvidenceCapability;

        // Prefer the action target the user names first. A task request often explains that
        // the tasks are "de khoi tao du an"; treating that purpose clause as the primary
        // action incorrectly opens Project Launch instead of Task Composer. Conversely,
        // "khoi tao du an ... sau do tao task" is still a Project Launch request. Resolve
        // this before staffing keywords because a launch request legitimately mentions team.
        if (taskActionIndex >= 0 || projectLaunchActionIndex >= 0)
        {
            if (taskActionIndex >= 0 &&
                (projectLaunchActionIndex < 0 || taskActionIndex < projectLaunchActionIndex))
                return AiAssistantContextContract.TaskCreateCapability;
            if (projectLaunchActionIndex >= 0)
                return AiAssistantContextContract.ProjectLaunchCapability;
        }

        var asksForStaffingPlan = staffingIndex >= 0 ||
            (ContainsAny(normalized, "manager", "team") &&
             ContainsAny(normalized, "skill evidence", "capacity", "lich vang", "sprint", "required skill") &&
             ContainsAny(normalized, "phuong an", "de xuat", "lap", "chon", "chi dinh", "can bang"));
        if (asksForStaffingPlan)
            return AiAssistantContextContract.ProjectStaffingPlanCapability;

        if (ContainsAny(normalized, "giu phuong an hien tai", "giu phuong an", "assignment final confirm") &&
            ContainsAny(normalized, "card xac nhan", "xac nhan cuoi", "khong ghi truoc", "final confirm"))
            return AiAssistantContextContract.TaskAssignmentScheduleCapability;

        var hasTaskNoun = ContainsAny(normalized, "task", "tasks", "cong viec", "nhiem vu");
        if (hasTaskNoun && (HasAffirmativeAction(normalized,
                ["assign", "phan cong", "gan cho", "giao", "can bang tai", "lap lich giao viec"]) ||
            FirstActionObjectIndex(normalized, ["gan"], "task", "tasks", "nhiem vu", "cong viec") >= 0))
            return AiAssistantContextContract.TaskAssignmentScheduleCapability;

        // An explicit request for useful read-only help when permission is missing is
        // still a read, not a denied draft. Evaluate this after affirmative draft targets.
        if (ContainsAny(normalized, "khong hien nut xac nhan mutation", "no mutation controls") ||
            (ContainsAny(normalized, "khong co quyen", "chua co quyen") &&
             ContainsAny(normalized, "van tra phan tich", "van tra loi", "van tom tat", "nut mo du lieu nguon")))
            return AiAssistantContextContract.GroundedReadCapability;

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
        => AiPromptLanguage.ContainsAny(value, terms);

    public static bool IsExplicitReadOnlyRequest(string? message)
    {
        // A no-write guard does not cancel an affirmative request to prepare a draft.
        return !TryGetUnsupportedAction(message ?? string.Empty, out _) &&
            (Infer(message ?? string.Empty) is AiAssistantContextContract.GroundedReadCapability or
                AiAssistantContextContract.ResearchPlanCapability);
    }

    public static bool IsSafeTestExecutionRequest(string? message)
    {
        var value = Normalize(message ?? string.Empty);
        if (!ContainsAny(value, "cand", "candidate", "candidates", "ai native", "test demo", "chay test", "run test", "run tests"))
            return false;
        var execution = FirstActionObjectIndex(value, ["chay", "run", "kiem thu", "test"],
            "test", "tests", "demo", "cand", "candidate", "candidates", "ai native");
        var task = FirstActionObjectIndex(value, CreateVerbs, "task", "tasks", "nhiem vu", "cong viec");
        return execution >= 0 && (task < 0 || execution < task);
    }

    public static bool TryGetUnsupportedAction(string message, out string action)
    {
        var value = Normalize(message);
        var candidates = new (string Label, string[] Verbs, string[] Objects)[]
        {
            ("tạo nhóm", CreateVerbs, ["nhom", "group", "team"]),
            ("tạo cuộc họp hoặc lịch", [.. CreateVerbs, "dat", "schedule"], ["cuoc hop", "lich hop", "meeting"]),
            ("tạo form hoặc quiz nhiều câu", CreateVerbs, ["form", "bieu mau", "quiz"]),
            ("cập nhật nhiệm vụ", ["cap nhat", "doi", "chuyen", "update", "change"], ["task", "nhiem vu", "trang thai", "status"])
        };
        foreach (var candidate in candidates)
        {
            if (FirstActionObjectIndex(value, candidate.Verbs, candidate.Objects) < 0) continue;
            action = candidate.Label;
            return true;
        }
        action = string.Empty;
        return false;
    }

    internal static string? TaskCreationCountTarget(string message)
    {
        var value = Normalize(message);
        var index = FirstActionObjectIndex(value, CreateVerbs, "task", "tasks", "nhiem vu", "cong viec");
        // Canonicalize just the target noun so the count parser can anchor immediately
        // before it; earlier counts describe context, not necessarily newly requested work.
        return index >= 0 ? value[..index] + "task" : null;
    }


    private static int FirstActionObjectIndex(
        string value,
        IReadOnlyList<string> actionVerbs,
        params string[] objectTerms)
    {
        // Keep the verb close to its object. A wider window makes unrelated customization
        // phrases such as "thêm/bớt người ... sửa Task" look like "thêm Task" and steals
        // the staffing route from the user's actual manager/team request.
        const int maximumActionDistance = 48;
        var first = -1;
        foreach (var objectTerm in objectTerms)
        {
            foreach (var objectIndex in AiPromptLanguage.PhraseIndexes(value, objectTerm))
            {
                foreach (var verb in actionVerbs)
                {
                    foreach (var actionIndex in AiPromptLanguage.PhraseIndexes(value, verb))
                    {
                        var actionEnd = actionIndex + verb.Length;
                        if (actionEnd > objectIndex || objectIndex - actionEnd > maximumActionDistance ||
                            IsNegatedAt(value, actionIndex)) continue;
                        var between = value[actionEnd..objectIndex];
                        // Do not steal an attribute of an earlier action target: "create 10
                        // tasks with acceptance criteria" is not "create acceptance criteria".
                        if (between.IndexOfAny([';', '.', '!', '?']) >= 0 ||
                            ContainsAny(between, "task", "tasks", "project", "du an", "nhiem vu",
                                "cong viec", "team", "manager", "sprint", "poll", "wiki")) continue;
                        if (objectTerm is "project" or "du an" &&
                            ContainsAny(between, "ke hoach", "phuong an", "cai thien", "bao cao")) continue;
                        if (first < 0 || objectIndex < first) first = objectIndex;
                    }
                }
            }
        }

        return first;
    }

    private static bool HasAffirmativeAction(string prefix, IReadOnlyList<string> actionVerbs)
    {
        return actionVerbs.Any(verb => AiPromptLanguage.PhraseIndexes(prefix, verb)
            .Any(index => !IsNegatedAt(prefix, index) &&
                !(verb == "giao" && Regex.IsMatch(prefix[(index + verb.Length)..], @"^\s+(?:dien|thong|tiep|dich)\b"))));
    }

    private static bool IsNegatedAction(string normalized, params string[] verbs)
    {
        return verbs.Any(verb => AiPromptLanguage.PhraseIndexes(normalized, verb)
            .Any(index => IsNegatedAt(normalized, index)));
    }

    private static bool IsNegatedAt(string value, int actionIndex)
    {
        var prefix = value[..actionIndex];
        var boundary = prefix.LastIndexOfAny([';', '.', '!', '?', ',']);
        prefix = prefix[(boundary + 1)..].Replace('/', ' ');
        // "Nhờ AI soạn" addresses the assistant; "ai soạn?" asks who did it.
        // Strip only an explicit assistant addressee in this routing copy, leaving
        // preceding negation or questions intact ("không nhờ AI", "vì sao trợ lý AI").
        prefix = Regex.Replace(prefix,
            @"\b(?:(?:tro ly|chatbot)\s+ai|(?:nho|yeu cau|muon|de|bao)\s+ai)\s*$", string.Empty);
        var markers = string.Join("|", NegationMarkers.Select(Regex.Escape));
        return Regex.IsMatch(prefix,
            $@"\b(?:{markers})(?:\s+(?:can|duoc|tu dong|xac nhan|khoi|tiep tuc|chay|run|tao|giao))*\s*$") ||
            Regex.IsMatch(prefix, @"\b(?:cach|huong dan|dao|tom|da|duoc|dang duoc|chua duoc|how to)\s*$") ||
            Regex.IsMatch(prefix, @"\b(?:ai|who|when|why|how|tai sao|vi sao|khi nao|bao gio|nguoi|nut|button)(?:\s+(?:da|dang|se|has|is|will))*\s*$") ||
            (Regex.IsMatch(prefix, $@"\b(?:{markers})\s+(?:tao|giao|chay|create|assign|run)\b.*\b(?:va|hoac|and|or)\s*$") &&
             !ContainsAny(prefix, "nhung", "thay vao do", "but", "instead", "hay"));
    }


    private static bool IsContinuation(string message)
    {
        var normalized = Normalize(message);
        return Regex.IsMatch(normalized,
            @"^(?:(?:ok|dong y)[,!. ]*)?(?:thu luon|thu nghiem luon|lam luon|lam di|bat dau di|tiep tuc|proceed|go ahead|ok|dong y|chay luon|trien khai luon)(?:\s+(?:di|nhe|nha|giup toi))?[.!?]*$");
    }

    private static bool IsContextReference(string message)
    {
        var normalized = Normalize(message);
        return normalized.Length <= 120 && ContainsAny(normalized,
            "tu do", "theo do", "cai do", "noi tren", "phan tren", "tai lieu nay", "wiki nay");
    }

    private static string Normalize(string value)
    {
        // Quoted titles/examples are data, not the action to execute. Keep apostrophes in
        // contractions (don't) while masking paired quotation marks in a routing copy only.
        var routingText = Regex.Replace(value,
            "\"[^\"]*\"|“[^”]*”|‘[^’]*’|`[^`]*`|(?<![\\p{L}\\p{N}])'[^']*'(?![\\p{L}\\p{N}])",
            match => new string(' ', match.Length));
        return AiPromptLanguage.Normalize(routingText);
    }
}
