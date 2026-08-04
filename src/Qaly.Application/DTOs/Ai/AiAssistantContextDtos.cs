using System.Globalization;
using System.Text;

namespace Qaly.Application.DTOs.Ai;

public static class AiAssistantContextContract
{
    public const string GroundedReadCapability = AiAssistantTurnContract.GroundedReadIntent;
    public const string ResearchPlanCapability = AiAssistantResearchPlanContract.CapabilityId;
    public const string TaskCreateCapability = AiAssistantTurnContract.TaskCreateIntent;

    public const string WorkspaceProjectsSource = "workspace.projects";
    public const string ProjectSummarySource = "project.summary";
    public const string ProjectTasksSource = "project.tasks";
    public const string ProjectWorkloadSource = "project.workload";
    public const string ProjectMembersSource = "project.members";
    public const string ProjectSkillsSource = "project.skills";
    public const string TaskDetailSource = "task.detail";

    public static readonly IReadOnlySet<string> KnownSourceIds = new HashSet<string>(StringComparer.Ordinal)
    {
        WorkspaceProjectsSource,
        ProjectSummarySource,
        ProjectTasksSource,
        ProjectWorkloadSource,
        ProjectMembersSource,
        ProjectSkillsSource,
        TaskDetailSource
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
                    AiAssistantContextContract.TaskDetailSource
                ],
                "read_only",
                "none",
                "reasoning_strong",
                "assistant-answer.v1",
                "AiJobsV4:AssistantContextRegistryEnabled",
                Title: "Tra cứu có căn cứ",
                Description: "Trả lời câu hỏi từ dữ liệu Qaly đã kiểm quyền và dẫn lại nguồn.",
                UserJobs: ["explain", "summarize", "inspect"],
                EntityTypes: ["workspace", "project", "task"]),
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
                    AiAssistantContextContract.TaskDetailSource
                ],
                "read_only_proposal",
                "explicit_adapter_handoff",
                "reasoning_strong",
                AiAssistantResearchPlanContract.RendererId,
                "AiJobsV4:AssistantResearchPlanEnabled",
                Title: "Phân tích và lập phương án",
                Description: "Tổng hợp facts, unknowns, options và action graph có nguồn để hỗ trợ quyết định.",
                UserJobs: ["analyze", "compare", "recommend", "plan"],
                EntityTypes: ["workspace", "project", "task"]),
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
                VerificationPolicy: "schema_source_permission_and_human_confirmation")
        };

    private static readonly HashSet<string> KnownSchemaIds = new(StringComparer.Ordinal)
    {
        "assistant_grounded_read_request.v1",
        "assistant_turn.v1",
        AiAssistantResearchPlanContract.RequestSchemaId,
        AiAssistantResearchPlanContract.SchemaId,
        "ai_action_compose_request.v1",
        "ai_action_intent_envelope.v1"
    };

    private static readonly HashSet<string> KnownRendererIds = new(StringComparer.Ordinal)
    {
        "assistant-answer.v1",
        AiAssistantResearchPlanContract.RendererId,
        "task-plan-review.v1"
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
    public static string Infer(string message)
    {
        var normalized = Normalize(message ?? string.Empty);
        var hasTaskNoun = ContainsAny(normalized, "task", "cong viec", "nhiem vu");
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
