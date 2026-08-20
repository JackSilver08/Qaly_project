using System.Text.Json;
using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiActionComposerContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MemberId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SkillId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly string ProjectRef = $"/projects/{ProjectId:D}";
    private static readonly string MemberRef = $"/projects/{ProjectId:D}/members/{MemberId:D}";
    private static readonly string SkillRef = $"/organizations/{OrganizationId:D}/skills/{SkillId:D}";

    [Fact]
    public void TryBuildResult_AuthorizedCommands_ProducesCanonicalReviewablePlan()
    {
        var valid = AiActionComposerOutputContract.TryBuildResult(
            ProviderPlan(),
            SnapshotJson(),
            out var resultJson,
            out var error);

        valid.Should().BeTrue(error);
        var result = JsonSerializer.Deserialize<AiActionPlanDto>(resultJson, JsonOptions)!;
        result.SchemaId.Should().Be(AiActionComposerContract.SchemaId);
        result.ProjectId.Should().Be(ProjectId);
        result.SourceVersion.Should().Be("source-v1");
        result.TargetEntities.Should().ContainSingle().Which.Label.Should().Be("Qaly Native AI");
        result.Review.SelectedOptionId.Should().Be("balanced");
        result.Review.SelectedCommandIds.Should().Equal("task-1");
        result.Options[0].Commands[0].ToolName.Should().Be(AiActionComposerContract.TaskCreateTool);
        result.Options[0].Commands[0].RequiredSkills.Should().ContainSingle()
            .Which.RequiredLevel.Should().Be("Proficient");
        AiActionComposerOutputContract.TryValidateFinal(resultJson, out error).Should().BeTrue(error);
    }

    [Fact]
    public void TryBuildDeterministicFallback_ProducesSafeReviewableTasksWithoutInventedAssignments()
    {
        var valid = AiActionComposerOutputContract.TryBuildDeterministicFallback(
            SnapshotJson(),
            out var resultJson,
            out var error);

        valid.Should().BeTrue(error);
        var result = JsonSerializer.Deserialize<AiActionPlanDto>(resultJson, JsonOptions)!;
        result.Options.Should().ContainSingle();
        result.Options[0].Commands.Should().HaveCount(3);
        result.Options[0].Commands.Should().OnlyContain(command =>
            command.AssigneeId == null &&
            command.AssigneeMode == "unassigned" &&
            command.RequiredSkills.Count == 0 &&
            command.SourceRefs.SequenceEqual(new[] { ProjectRef }));
        result.Warnings.Should().ContainSingle(message => message.Contains("server", StringComparison.OrdinalIgnoreCase));
        AiActionComposerOutputContract.TryValidateFinal(resultJson, out error).Should().BeTrue(error);
    }

    [Theory]
    [InlineData("tạo 10 task cho sprint 1", 10)]
    [InlineData("Create 12 tasks for the first phase", 12)]
    [InlineData("tạo 50 nhiệm vụ", 50)]
    [InlineData("tạo mười task cho giai đoạn đầu", 10)]
    [InlineData("tạo mười hai công việc", 12)]
    [InlineData("create twenty tasks", 20)]
    [InlineData("phân tích sprint hiện tại", null)]
    public void ExtractRequestedTaskCount_UnderstandsExplicitNaturalLanguageCount(string message, int? expected)
        => AiActionComposerService.ExtractRequestedTaskCount(message).Should().Be(expected);

    [Fact]
    public void ExplicitTenTaskIntent_IsPreservedByContractAndFallback()
    {
        AiActionComposerOutputContract.TryBuildResult(
            ProviderPlan(),
            SnapshotJson(10),
            out _,
            out var providerError).Should().BeFalse();
        providerError.Should().Contain("exactly the 10 task commands");

        AiActionComposerOutputContract.TryBuildDeterministicFallback(
            SnapshotJson(10),
            out var fallbackJson,
            out var fallbackError).Should().BeTrue(fallbackError);
        var fallback = JsonSerializer.Deserialize<AiActionPlanDto>(fallbackJson, JsonOptions)!;
        fallback.Options[0].Commands.Should().HaveCount(10);
        fallback.Options[0].Commands.Select(item => item.CommandId).Should().OnlyHaveUniqueItems();
        fallback.Options[0].Commands.Select(item => item.Title).Should().OnlyHaveUniqueItems();
        fallback.Review.SelectedCommandIds.Should().HaveCount(10);
    }

    [Fact]
    public void TryBuildResult_InventedTool_FailsClosed()
    {
        var output = ProviderPlan(toolName: "project.delete.v1");

        AiActionComposerOutputContract.TryBuildResult(
            output,
            SnapshotJson(),
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("task.create.v1");
    }

    [Fact]
    public void TryBuildResult_UnknownMemberSkillOrSource_FailsClosed()
    {
        var outsider = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        AiActionComposerOutputContract.TryBuildResult(
            ProviderPlan(assigneeId: outsider),
            SnapshotJson(),
            out _,
            out var memberError).Should().BeFalse();
        memberError.Should().Contain("model cannot assign");

        AiActionComposerOutputContract.TryBuildResult(
            ProviderPlan(skillId: outsider),
            SnapshotJson(),
            out _,
            out var skillError).Should().BeFalse();
        skillError.Should().Contain("skill");

        AiActionComposerOutputContract.TryBuildResult(
            ProviderPlan(sourceRefs: ["/projects/other"]),
            SnapshotJson(),
            out _,
            out var sourceError).Should().BeFalse();
        sourceError.Should().Contain("authorized sources");
    }

    [Fact]
    public void TryValidateReviewedPlan_ExplicitUserAssignee_IsAllowedButUnknownCommandIsRejected()
    {
        AiActionComposerOutputContract.TryBuildResult(
            ProviderPlan(),
            SnapshotJson(),
            out var resultJson,
            out var buildError).Should().BeTrue(buildError);
        var plan = JsonSerializer.Deserialize<AiActionPlanDto>(resultJson, JsonOptions)!;
        var command = plan.Options[0].Commands[0] with
        {
            AssigneeId = MemberId,
            AssigneeMode = "user_selected",
            Title = "User reviewed task title"
        };
        plan = plan with
        {
            Options = [plan.Options[0] with { Commands = [command] }]
        };
        var reviewedJson = JsonSerializer.Serialize(plan, JsonOptions);
        AiActionComposerOutputContract.TryReadSnapshot(
            SnapshotJson(),
            out var snapshot,
            out var snapshotError).Should().BeTrue(snapshotError);

        AiActionComposerOutputContract.TryValidateReviewedPlan(
            reviewedJson,
            snapshot!,
            out var reviewed,
            out var reviewError).Should().BeTrue(reviewError);
        reviewed!.Options[0].Commands[0].AssigneeId.Should().Be(MemberId);

        var invalid = plan with
        {
            Review = plan.Review with { SelectedCommandIds = ["missing-command"] }
        };
        AiActionComposerOutputContract.TryValidateReviewedPlan(
            JsonSerializer.Serialize(invalid, JsonOptions),
            snapshot!,
            out _,
            out var invalidError).Should().BeFalse();
        invalidError.Should().Contain("unknown command");
    }

    [Theory]
    [InlineData("project")]
    [InlineData("source")]
    [InlineData("intent")]
    public void TryValidateReviewedPlan_ProtectedContractFieldsCannotBeChanged(string protectedField)
    {
        AiActionComposerOutputContract.TryBuildResult(
            ProviderPlan(),
            SnapshotJson(),
            out var resultJson,
            out var buildError).Should().BeTrue(buildError);
        var plan = JsonSerializer.Deserialize<AiActionPlanDto>(resultJson, JsonOptions)!;
        plan = protectedField switch
        {
            "project" => plan with { ProjectId = Guid.NewGuid() },
            "source" => plan with { SourceVersion = "stale-source" },
            _ => plan with { IntentType = "project.delete" }
        };
        AiActionComposerOutputContract.TryReadSnapshot(
            SnapshotJson(),
            out var snapshot,
            out _).Should().BeTrue();

        AiActionComposerOutputContract.TryValidateReviewedPlan(
            JsonSerializer.Serialize(plan, JsonOptions),
            snapshot!,
            out _,
            out var error).Should().BeFalse();

        error.Should().Contain("protected");
    }

    private static string SnapshotJson(int? requestedTaskCount = null)
        => JsonSerializer.Serialize(new AiActionContextSnapshotDto(
            AiActionComposerContract.SnapshotSchemaId,
            new AiActionProjectContextDto(
                ProjectId,
                OrganizationId,
                "Qaly Native AI",
                "QNA",
                "Active",
                DateTimeOffset.UtcNow.AddDays(-10),
                DateTimeOffset.UtcNow.AddDays(60),
                ProjectRef),
            "source-v1",
            "vi",
            3,
            "Tách module đăng nhập thành task frontend, backend và QA.",
            [new AiActionMemberContextDto(MemberId, "Project Manager", "Manager", 2, 12, MemberRef)],
            [new AiActionSkillContextDto(SkillId, "Vue.js", "Frontend engineering", SkillRef)],
            [ProjectRef, MemberRef, SkillRef],
            null,
            requestedTaskCount),
            JsonOptions);

    private static string ProviderPlan(
        string toolName = AiActionComposerContract.TaskCreateTool,
        Guid? assigneeId = null,
        Guid? skillId = null,
        IReadOnlyList<string>? sourceRefs = null)
    {
        var command = new AiActionTaskCommandDto(
            "task-1",
            toolName,
            "1.0",
            "Implement secure login UI",
            "Create the Vue login flow.",
            ["User can sign in", "Permission failures are visible"],
            "High",
            DateTimeOffset.UtcNow.AddDays(7),
            8,
            assigneeId,
            assigneeId.HasValue ? "workload_only" : "unassigned",
            [new AiActionSkillSelectionDto(skillId ?? SkillId, "proficient")],
            sourceRefs ?? [ProjectRef, SkillRef],
            []);
        var plan = new AiActionPlanDto(
            AiActionComposerContract.SchemaId,
            "1.0",
            ProjectId,
            "source-v1",
            "Provider text that must be replaced by server intent",
            "task.create",
            0.876m,
            [],
            ["Existing auth API is available."],
            [],
            [],
            [new AiActionOptionDto("balanced", "Balanced", "One focused implementation task.", [], [command])],
            new AiActionReviewSelectionDto("ignored", []),
            DateTimeOffset.UtcNow);
        return JsonSerializer.Serialize(plan, JsonOptions);
    }
}
