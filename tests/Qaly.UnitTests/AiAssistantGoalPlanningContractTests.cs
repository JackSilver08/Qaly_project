using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Interfaces;

#pragma warning disable CA1861

namespace Qaly.UnitTests;

public sealed class AiAssistantGoalPlanningContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void BuildResult_ReconcilesModelScopeAndSelectsOnlyAuthorizedRegisteredSkill()
    {
        var projectId = Guid.NewGuid();
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.ResearchPlanCapability, out var research).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            "Phân tích và đề xuất phương án",
            new AiAssistantClientContextDto("/projects", "project", projectId),
            null,
            [research!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson("Phân tích dự án", research!.CapabilityId, modelProjectId: Guid.NewGuid()),
            JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().Be(research.CapabilityId);
        result.GoalAnalysis.Scopes.Single().ProjectId.Should().Be(projectId);
        result.GoalAnalysis.ActualProvider.Should().Be("DeepSeek");
        result.WorkPlan.Steps.Should().ContainSingle(step => step.Kind == "call_skill");
    }

    [Fact]
    public void BuildResult_P02WorkspaceRead_CannotBeEscalatedByModelToTaskMutation()
    {
        const string prompt = "Tóm tắt workspace hiện tại: số Project đang hoạt động, tiến độ, task quá hạn, workload cao và ba việc cần chú ý. Dùng metric/table/card phù hợp, có link nguồn; phần quy trình collapse mặc định.";
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.GroundedReadCapability, out var read).Should().BeTrue();
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.TaskCreateCapability, out var taskCreate).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            prompt,
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            null,
            [read!, taskCreate!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson(prompt, taskCreate!.CapabilityId),
            JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        result.GoalAnalysis.Disposition.Should().Be("answerable");
        result.GoalAnalysis.RequiresConfirmation.Should().BeFalse();
        result.WorkPlan.Steps.Should().NotContain(step => step.MutationClass != "none");
    }

    [Fact]
    public void BuildResult_P03ProjectRead_UsesAuthorizedReadWhenModelMutationIsUnavailable()
    {
        const string prompt = "Phân tích Project đang chọn: mục tiêu, tiến độ Sprint, task nghẽn, dependency, workload, rủi ro deadline và ba hành động ưu tiên. Chỉ dùng dữ liệu tôi được phép xem.";
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.GroundedReadCapability, out var read).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            prompt,
            new AiAssistantClientContextDto("/groups/group-id", "groups", Guid.NewGuid(), "group", Guid.NewGuid()),
            null,
            [read!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson(prompt, AiAssistantContextContract.TaskCreateCapability),
            JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().Be(AiAssistantContextContract.GroundedReadCapability);
        result.GoalAnalysis.Disposition.Should().Be("answerable");
        result.GoalAnalysis.RequiresConfirmation.Should().BeFalse();
        result.WorkPlan.Steps.Should().NotContain(step => step.MutationClass != "none");
    }

    [Fact]
    public void BuildResult_ModelInventedSkill_IsReportedButNeverSelected()
    {
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.GroundedReadCapability, out var read).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            "Chạy test demo tất cả CAND", new AiAssistantClientContextDto("/dashboard", "workspace"), null, [read!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson("Chạy test demo", "shell.execute.v1", missingSkillId: "demo.test.run.v1"),
            JsonSerializer.Serialize(context, JsonOptions), "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().BeNull();
        result.GoalAnalysis.Disposition.Should().Be("unsupported_but_analyzed");
        result.GoalAnalysis.MissingSkills.Should().Contain(item => item.SkillId == "demo.test.run.v1");
        result.WorkPlan.Steps.Should().NotContain(step => step.Kind == "call_skill");
    }

    [Fact]
    public void DeterministicFallback_DemoRequest_IsHonestAndNeverExecutes()
    {
        var context = new AiAssistantExecutionContextDto(WithoutSafeTestCapability(), [], []);
        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto("Chạy test demo tất cả CAND đã implement"), context, "provider_unavailable");

        result.UsedFallback.Should().BeTrue();
        result.SelectedCapabilityId.Should().BeNull();
        result.GoalAnalysis.ActualProvider.Should().Be("not_reached");
        result.GoalAnalysis.MissingSkills.Should().Contain(item => item.SkillId == "demo.test.run.v1");
    }

    [Fact]
    public void BuildResult_ModelSelectsReadSkillForDemoExecution_ServerVetoesHandoff()
    {
        AiAssistantCapabilityCatalog.TryGet(AiAssistantContextContract.GroundedReadCapability, out var read).Should().BeTrue();
        var context = new AiAssistantGoalPlanningValidationContextDto(
            "chạy tự động để test các CAND đã implement",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            null,
            [read!]);

        var ok = AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson("Test implemented candidates", read!.CapabilityId),
            JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-v4-pro",
            out var result, out var error);

        ok.Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().BeNull();
        result.GoalAnalysis.Disposition.Should().Be("unsupported_but_analyzed");
        result.GoalAnalysis.MissingSkills.Should().ContainSingle(item => item.SkillId == "demo.test.run.v1");
        result.WorkPlan.Steps.Should().NotContain(step => step.Kind == "call_skill");
    }

    [Fact]
    public void DeterministicFallback_NaturalProjectPhrase_SelectsArtifactOnlyProjectLaunchSkill()
    {
        var context = new AiAssistantExecutionContextDto(WithoutSafeTestCapability(), [], []);

        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto(
                "Tạo một dự án web SPA",
                new AiAssistantClientContextDto("/dashboard", "projects")),
            context,
            "goal_provider_unavailable");

        result.SelectedCapabilityId.Should().Be(AiProjectLaunchContract.CapabilityId);
        result.GoalAnalysis.Disposition.Should().Be("plannable");
        result.GoalAnalysis.MissingSkills.Should().BeEmpty();
        result.WorkPlan.Steps.Should().Contain(step =>
            step.Kind == "call_skill" && step.SkillId == AiProjectLaunchContract.CapabilityId && step.MutationClass == "none");
    }

    [Fact]
    public void IntentClassifier_CreateProjectWithStaffingDetails_StartsWithLaunchBrief()
    {
        var capability = AiAssistantCapabilityIntentClassifier.Infer(
            "Khởi chạy dự án web SPA, chỉ định manager, thành viên, sprint và phân việc");

        capability.Should().Be(AiProjectLaunchContract.CapabilityId);
    }

    [Fact]
    public void IntentClassifier_CreateTasksForProjectStartup_SelectsTaskComposer()
    {
        var capability = AiAssistantCapabilityIntentClassifier.Infer(
            "giup toi tao mot loat cac task cho giai doan dau, sprint 1, khao sat va tim tai lieu de khoi tao du an");

        capability.Should().Be(AiAssistantContextContract.TaskCreateCapability);
    }

    [Theory]
    [InlineData("Soan acceptance checklist nghiem thu cho task nay", AiAssistantContextContract.AcceptanceChecklistCapability)]
    [InlineData("Tach task nay thanh 10 subtask", AiAssistantContextContract.TaskBreakdownCapability)]
    [InlineData("Tu wiki nay tao task theo doi", AiAssistantContextContract.WikiBriefTaskCapability)]
    [InlineData("Tao poll binh chon trong group", AiAssistantContextContract.GroupPollCapability)]
    [InlineData("Tat weekly digest cho du an", AiAssistantContextContract.ProjectDigestCapability)]
    public void IntentClassifier_NativeDomainActions_SelectExactCapability(string message, string expectedCapability)
    {
        AiAssistantCapabilityIntentClassifier.Infer(message).Should().Be(expectedCapability);
    }

    [Fact]
    public void IntentClassifier_P28ExternalAdapters_UsesDeterministicReadOnlyStatusPath()
    {
        const string prompt = "Kiem tra kha nang dong bo calendar, repository, invitation, webhook va deployment cho Project nay. " +
                              "Chi danh dau hoan thanh neu adapter that da doc/ghi va read-back; phan chua co phai ghi EXTERNAL_DEFERRED.";

        AiAssistantCapabilityIntentClassifier.IsExternalAdapterStatusQuery(prompt).Should().BeTrue();
        AiAssistantCapabilityIntentClassifier.Infer(prompt)
            .Should().Be(AiAssistantContextContract.GroundedReadCapability);
    }

    [Theory]
    [InlineData("Tom tat Wiki dang mo thanh brief co link section nguon; de xuat toi da 3 Task tuy chon, chi tao cac Task toi tick chon.", AiAssistantContextContract.WikiBriefTaskCapability)]
    [InlineData("Trong Group dang mo, soan Poll voi 4 option ro rang, deadline 3 ngay va cho sua truoc khi xac nhan.", AiAssistantContextContract.GroupPollCapability)]
    [InlineData("Tu transcript cuoc hop dang mo, trich quyet dinh, blocker va action item; map sang Task co san hoac Task moi.", AiAssistantContextContract.MeetingActionsCapability)]
    [InlineData("Danh gia roadmap Project hien tai va de xuat dieu chinh Sprint theo dependency, capacity va deadline; hien before/after.", AiAssistantContextContract.RoadmapAdjustCapability)]
    [InlineData("Cau hinh weekly digest cho Project nay vao 09:00 thu Hai theo timezone cua to chuc.", AiAssistantContextContract.ProjectDigestCapability)]
    [InlineData("Voi Task vua hoan tat, de xuat attribution va skill evidence theo tieu chi nghiem thu da xac nhan.", AiAssistantContextContract.SkillEvidenceCapability)]
    [InlineData("So sanh Project hien tai voi baseline da xac nhan, chi ra drift va de xuat replan before/after.", AiAssistantContextContract.ProjectOperationMonitorCapability)]
    public void IntentClassifier_P18ToP24_SelectsExactCapability(string message, string expectedCapability)
    {
        AiAssistantCapabilityIntentClassifier.Infer(message).Should().Be(expectedCapability);
    }

    [Fact]
    public void IntentClassifier_ProjectLaunchBeforeTaskBreakdown_SelectsProjectLaunch()
    {
        var capability = AiAssistantCapabilityIntentClassifier.Infer(
            "khoi tao du an web SPA, sau do tao task chi tiet theo sprint");

        capability.Should().Be(AiProjectLaunchContract.CapabilityId);
    }

    [Theory]
    [InlineData(
        "Lập ba phương án manager/team dựa trên skill evidence, capacity đã khai báo, lịch vắng và tải đa dự án. Sau đó chia phase, Sprint, Task, dependency, estimate, required skill và assignee.",
        AiProjectOrchestrationContract.StaffingCapabilityId)]
    [InlineData(
        "Dùng phương án đang chọn. Trước khi ghi hãy hiện một card review cuối gồm Project, manager/team, phase, Sprint và tổng số Task. Chờ đúng một xác nhận của tôi.",
        AiProjectOrchestrationContract.ExecuteCapabilityId)]
    [InlineData(
        "Mở lại kết quả thực thi Project vừa rồi và kiểm tra xem retry cùng yêu cầu có tạo trùng Project, Sprint hoặc Task không. Chỉ báo theo dữ liệu đọc lại.",
        AiProjectOrchestrationContract.MonitorCapabilityId)]
    public void IntentClassifier_P08ToP10_SelectsExactProjectOrchestrationCapability(
        string message,
        string expectedCapability)
    {
        AiAssistantCapabilityIntentClassifier.Infer(message).Should().Be(expectedCapability);
    }

    [Fact]
    public void DeterministicFallback_CreateTasksMentioningProjectPurpose_SelectsTaskComposer()
    {
        var context = new AiAssistantExecutionContextDto(AiAssistantCapabilityCatalog.All.ToArray(), [], []);
        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto(
                "giup toi tao task cho sprint 1 de khoi tao du an",
                new AiAssistantClientContextDto("/projects/project-id", "project", Guid.NewGuid())),
            context,
            "goal_provider_unavailable");

        result.SelectedCapabilityId.Should().Be(AiAssistantContextContract.TaskCreateCapability);
    }

    [Fact]
    public void IntentClassifier_PoliteProjectActionAfterCapabilityMenu_IsNotMistakenForAnotherOverview()
    {
        var message = "Bạn có thể giúp tôi khởi tạo 1 dự án về web cung cấp dịch vụ spa theo gói được không?";
        var history = new List<AiChatMessageDto>
        {
            new("user", "Bạn có thể giúp cho tôi những gì?"),
            new("assistant", "Mình có thể hỗ trợ bạn theo 5 hướng chính.")
        };

        AiAssistantCapabilityIntentClassifier.IsCapabilityOverviewQuery(message).Should().BeFalse();
        AiAssistantCapabilityIntentClassifier.Infer(message, history)
            .Should().Be(AiProjectLaunchContract.CapabilityId);
    }

    [Theory]
    [InlineData("Không tạo task, chỉ phân tích tiến độ và task quá hạn.", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Đừng giao task; hãy liệt kê các task chưa được giao.", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Những task này đang được giao cho ai?", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Don't create tasks; just summarize overdue work.", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Không điều chỉnh roadmap, chỉ phân tích rủi ro deadline.", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Không xác nhận tạo Project; chỉ xem lại phương án đang chọn.", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Capacity đã khai báo của team hiện còn bao nhiêu?", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Dùng weekly digest để xem báo cáo nào đã được cấu hình.", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Hãy tạo bản nháp đúng 4 task, không ghi dữ liệu cho tới khi tôi xác nhận.", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Tạoooo 3 task cho Sprint hiện tại.", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Không tạo Project; hãy tạo 3 task cho Project đang chọn.", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Không tạo task; hãy khởi chạy Project web SPA.", AiAssistantContextContract.ProjectLaunchCapability)]
    [InlineData("Giao task này cho An sau khi kiểm tra capacity.", AiAssistantContextContract.TaskAssignmentScheduleCapability)]
    public void IntentClassifier_AdversarialLanguage_PreservesReadAndMutationBoundaries(
        string message,
        string expectedCapability)
    {
        AiAssistantCapabilityIntentClassifier.Infer(message).Should().Be(expectedCapability);
    }

    [Fact]
    public void IntentClassifier_ShortContinuation_UsesRecentProjectLaunchContext()
    {
        var history = new List<AiChatMessageDto>
        {
            new("user", "Mình muốn tự động tạo dự án web SPA"),
            new("assistant", "Mình có thể lập Project Launch Brief và phương án manager/team cho bạn.")
        };

        var capability = AiAssistantCapabilityIntentClassifier.Infer("thử luôn", history);

        capability.Should().Be(AiProjectLaunchContract.CapabilityId);
    }

    [Fact]
    public void IntentClassifier_Continuation_UsesNewestRelevantTopicInsteadOfOlderLaunchMention()
    {
        var history = new List<AiChatMessageDto>
        {
            new("user", "Khởi chạy Project web SPA"),
            new("assistant", "Mình đã chuẩn bị Project Launch Brief."),
            new("user", "Tóm tắt wiki kiến trúc và đề xuất task theo dõi"),
            new("assistant", "Mình đã mở bản nháp từ Wiki với nguồn section-level.")
        };

        AiAssistantCapabilityIntentClassifier.Infer("tiếp tục", history)
            .Should().Be(AiAssistantContextContract.WikiBriefTaskCapability);
    }

    [Fact]
    public void IntentClassifier_GenericContinuationWithoutHistory_FailsSafeToRead()
    {
        AiAssistantCapabilityIntentClassifier.Infer("thử luôn", [])
            .Should().Be(AiAssistantContextContract.GroundedReadCapability);
    }

    [Fact]
    public void IntentClassifier_UnrelatedQuestion_DoesNotStayLockedToOldLaunchContext()
    {
        var history = new List<AiChatMessageDto>
        {
            new("user", "Mình muốn tự động tạo dự án web SPA"),
            new("assistant", "Mình có thể lập Project Launch Brief cho bạn.")
        };

        var capability = AiAssistantCapabilityIntentClassifier.Infer("Tiến độ workspace tuần này thế nào?", history);

        capability.Should().Be(AiAssistantContextContract.GroundedReadCapability);
    }

    [Fact]
    public void AuthorizedExecutionPlan_ExplicitContinuation_DoesNotDependOnModelRanking()
    {
        var context = new AiAssistantExecutionContextDto(AiAssistantCapabilityCatalog.All.ToArray(), [], []);
        var request = new AiAssistantTurnRequestDto(
            "Tiếp tục lập staffing và delivery plan",
            new AiAssistantClientContextDto("/dashboard", "workspace"),
            RequestedCapabilityId: AiProjectOrchestrationContract.StaffingCapabilityId);

        var ok = AiAssistantGoalPlanningOutputContract.TryCreateAuthorizedExecutionPlan(
            request, context, out var result);

        ok.Should().BeTrue();
        result!.SelectedCapabilityId.Should().Be(AiProjectOrchestrationContract.StaffingCapabilityId);
        result.GoalAnalysis.ActualProvider.Should().Be("Qaly capability router");
        result.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Planner_ProjectLaunchAction_RoutesWithoutCallingPlannerProvider()
    {
        var gateway = new Mock<IAiGateway>(MockBehavior.Strict);
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        var planner = new AiAssistantGoalPlanner(
            gateway.Object,
            currentUser.Object,
            Options.Create(new AiJobPlatformOptions { AssistantGoalPlannerEnabled = true }));
        var context = new AiAssistantExecutionContextDto(AiAssistantCapabilityCatalog.All.ToArray(), [], []);

        var result = await planner.PlanAsync(
            new AiAssistantTurnRequestDto("Tạo dự án web SPA và chỉ định manager phù hợp"),
            context);

        result.IsSuccess.Should().BeTrue();
        result.Data!.SelectedCapabilityId.Should().Be(AiProjectLaunchContract.CapabilityId);
        gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Planner_KnownDemoExecutionRequest_DoesNotCallProvider()
    {
        var gateway = new Mock<IAiGateway>(MockBehavior.Strict);
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        var planner = new AiAssistantGoalPlanner(
            gateway.Object,
            currentUser.Object,
            Options.Create(new AiJobPlatformOptions { AssistantGoalPlannerEnabled = true }));
        var context = new AiAssistantExecutionContextDto(WithoutSafeTestCapability(), [], []);

        var result = await planner.PlanAsync(
            new AiAssistantTurnRequestDto(
                "chạy tự động để test các CAND đã implement",
                new AiAssistantClientContextDto("/dashboard", "workspace")),
            context);

        result.IsSuccess.Should().BeTrue();
        result.Data!.SelectedCapabilityId.Should().BeNull();
        result.Data.GoalAnalysis.ActualProvider.Should().Be("Qaly policy router");
        gateway.VerifyNoOtherCalls();
    }

    [Fact]
    public void DeterministicFallback_DemoRequest_WithDevCapability_PreparesConfirmedAdapterHandoff()
    {
        var context = new AiAssistantExecutionContextDto(AiAssistantCapabilityCatalog.All.ToArray(), [], []);

        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto("Chạy test demo tất cả CAND đã implement"), context, "provider_unavailable");

        result.SelectedCapabilityId.Should().Be(AiSafeTestOrchestratorContract.CapabilityId);
        result.GoalAnalysis.MissingSkills.Should().BeEmpty();
        result.GoalAnalysis.RequiresConfirmation.Should().BeTrue();
        result.WorkPlan.Steps.Should().Contain(step =>
            step.Kind == "call_skill" && step.SkillId == AiSafeTestOrchestratorContract.CapabilityId);
    }

    [Fact]
    public void ValidateModel_RejectsCyclicWorkPlan()
    {
        var valid = AiAssistantGoalPlanningOutputContract.TryValidateModel(
            ModelJson("Analyze", AiAssistantContextContract.GroundedReadCapability, selfCycle: true),
            null,
            out var error);

        valid.Should().BeFalse();
        error.Should().Contain("cycle");
    }

    [Theory]
    [InlineData(AiAssistantContextContract.ProjectLaunchCapability)]
    [InlineData(AiAssistantContextContract.ProjectStaffingPlanCapability)]
    [InlineData(AiAssistantContextContract.TaskCreateCapability)]
    public void ReadPrompt_ModelCannotSelectActionEvenWhenArtifactHasNoMutationRisk(string capabilityId)
    {
        const string prompt = "Phân tích tiến độ Project hiện tại";
        var context = new AiAssistantGoalPlanningValidationContextDto(
            prompt, null, null, AiAssistantCapabilityCatalog.All.ToArray());
        AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson(prompt, capabilityId), JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-chat", out var result, out var error).Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().Be(AiAssistantContextContract.GroundedReadCapability);
    }

    [Theory]
    [InlineData("Soạn 4 Task cho Project", AiAssistantContextContract.TaskCreateCapability, AiAssistantContextContract.ProjectLaunchCapability)]
    [InlineData("Phân tích tiến độ Project", AiAssistantContextContract.GroundedReadCapability, AiAssistantContextContract.ProjectLaunchCapability)]
    public void ModelCannotSubstituteAnotherActionWhenIntendedCapabilityIsUnavailable(string prompt, string deniedId, string substituteId)
    {
        var context = new AiAssistantGoalPlanningValidationContextDto(prompt, null, null,
            AiAssistantCapabilityCatalog.All.Where(item => item.CapabilityId != deniedId).ToArray());
        AiAssistantGoalPlanningOutputContract.TryBuildResult(
            ModelJson(prompt, substituteId), JsonSerializer.Serialize(context, JsonOptions),
            "DeepSeek", "deepseek-chat", out var result, out var error).Should().BeTrue(error);
        result!.SelectedCapabilityId.Should().BeNull();
        result.GoalAnalysis.Disposition.Should().Be("policy_blocked");
        result.GoalAnalysis.MissingSkills.Should().Contain(item => item.SkillId == deniedId);
        result.WorkPlan.Steps.Should().NotContain(item => item.Kind == "call_skill");
    }

    private static AiAssistantCapabilityDescriptorDto[] WithoutSafeTestCapability()
        => AiAssistantCapabilityCatalog.All
            .Where(item => item.CapabilityId != AiSafeTestOrchestratorContract.CapabilityId)
            .ToArray();

    private static string ModelJson(
        string objective,
        string rankedSkillId,
        Guid? modelProjectId = null,
        string? missingSkillId = null,
        bool selfCycle = false)
    {
        var scope = new
        {
            scopeType = "project", projectId = modelProjectId, entityType = (string?)null, entityId = (Guid?)null,
            label = "Model scope", confidence = 0.8, reason = "Model guess"
        };
        return JsonSerializer.Serialize(new
        {
            schemaId = AiAssistantGoalPlanningContract.SchemaId,
            promptId = AiAssistantGoalPlanningContract.PromptId,
            promptVersion = AiAssistantGoalPlanningContract.PromptVersion,
            objective,
            userJob = objective,
            intentFacets = new[] { "analysis" },
            scopes = new[] { scope },
            constraints = Array.Empty<string>(),
            unknowns = Array.Empty<object>(),
            assumptions = Array.Empty<string>(),
            rankedSkills = new[] { new { skillId = rankedSkillId, fitReason = "Best fit", confidence = 0.9 } },
            missingSkills = missingSkillId == null
                ? Array.Empty<object>()
                : new object[] { new { skillId = missingSkillId, title = "Missing skill", reason = "Not registered", suggestedPath = "Add an adapter" } },
            riskLevel = "low",
            requiresConfirmation = false,
            disposition = missingSkillId == null ? "plannable" : "unsupported_but_analyzed",
            confidence = 0.9,
            warnings = Array.Empty<string>(),
            workPlan = new
            {
                schemaId = AiAssistantGoalPlanningContract.WorkPlanSchemaId,
                objective,
                scope,
                selectedSkillIds = new[] { rankedSkillId },
                steps = new[]
                {
                    new { stepId = "S1", kind = "analyze", publicLabel = "Analyze", skillId = (string?)null, sourceIds = Array.Empty<string>(), dependencyIds = selfCycle ? new[] { "S1" } : Array.Empty<string>(), expectedOutputSchemaId = (string?)null, verificationIds = new[] { "contract" }, mutationClass = "none", state = "planned" }
                },
                blockingUnknowns = Array.Empty<object>(),
                maxSteps = 8,
                maxAttemptsPerStep = 2,
                stopConditions = new[] { "policy_denied" },
                requiresPlanApproval = false
            }
        }, JsonOptions);
    }
}
