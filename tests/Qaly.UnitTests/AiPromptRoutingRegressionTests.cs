using FluentAssertions;
using Microsoft.Extensions.AI;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

public sealed class AiPromptRoutingRegressionTests
{
    [Theory]
    [InlineData("Tóm tắt weekly digest cho Project này")]
    [InlineData("Phân tích task đào tạo nhân sự")]
    [InlineData("Chỉ phân tích cách tạo task cho dự án, không thao tác")]
    [InlineData("Đừng tạo Project")]
    [InlineData("Không tạo task")]
    [InlineData("Đừng khởi tạo dự án")]
    [InlineData("Không cần tự động giao task")]
    [InlineData("Chỉ kiểm tra capacity và mức độ phù hợp của team")]
    [InlineData("Xem nhiệm vụ đã giao cho An")]
    [InlineData("Chỉ xem trạng thái, đừng chạy test CAND")]
    [InlineData("Không chia Task này thành 4 subtask")]
    [InlineData("Phân tích task 'Tạo Project mới' đang quá hạn")]
    [InlineData("Giải thích câu \"Create a project\" nghĩa là gì")]
    [InlineData("Không tạo Project và giao task")]
    [InlineData("Tóm tắt Plan 18 và CAND")]
    [InlineData("Ai tạo Project này?")]
    [InlineData("Cho tôi biết ai tạo Project này?")]
    [InlineData("Giải thích vì sao trợ lý AI tạo Project mới")]
    [InlineData("Không nhờ AI tạo Project mới")]
    [InlineData("Người tạo dự án là ai?")]
    [InlineData("Ai phân công task này?")]
    [InlineData("Nút tạo Project hoạt động ra sao?")]
    [InlineData("Tóm tắt Project và tài liệu tôi được phép xem, sau đó đề xuất ba việc nên làm. Nếu tôi không có quyền tạo/giao Task, vẫn trả phân tích hữu ích và nút mở dữ liệu nguồn; không hiện nút xác nhận mutation.")]
    public void ReadAndNegatedRequests_DoNotSelectAction(string prompt)
        => AiAssistantCapabilityIntentClassifier.Infer(prompt)
            .Should().Be(AiAssistantContextContract.GroundedReadCapability);

    [Theory]
    [InlineData("Tóm tắt weekly digest cho Project này")]
    [InlineData("Phân tích workload của member")]
    [InlineData("Không tạo Project; chỉ phân tích tiến độ")]
    [InlineData("Chỉ xem trạng thái, đừng chạy test CAND")]
    public void ProviderFailure_DoesNotReintroduceKeywordActions(string prompt)
    {
        var context = new AiAssistantExecutionContextDto(AiAssistantCapabilityCatalog.All.ToArray(), [], []);
        var result = AiAssistantGoalPlanningOutputContract.CreateDeterministicFallback(
            new AiAssistantTurnRequestDto(prompt), context, "provider_unavailable");

        result.SelectedCapabilityId.Should().Be(AiAssistantContextContract.GroundedReadCapability);
    }

    [Theory]
    [InlineData("Phân tích rồi soạn 4 task, chưa ghi dữ liệu", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Tạo 10 Task có mô tả và tiêu chí nghiệm thu", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Soạn 5 mục acceptance checklist cho Task đang mở", AiAssistantContextContract.AcceptanceChecklistCapability)]
    [InlineData("Giao các task chưa giao cho An", AiAssistantContextContract.TaskAssignmentScheduleCapability)]
    [InlineData("Giao task hiện tại cho An; không tạo task mới", AiAssistantContextContract.TaskAssignmentScheduleCapability)]
    [InlineData("Tạo 3 task; không khởi tạo Project mới", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Dừng weekly digest cho Project này", AiAssistantContextContract.ProjectDigestCapability)]
    [InlineData("Create 4 tasks for this project", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Draft 5 acceptance checklist items for this task", AiAssistantContextContract.AcceptanceChecklistCapability)]
    [InlineData("Create a project then create tasks", AiAssistantContextContract.ProjectLaunchCapability)]
    [InlineData("Split this task into 4 subtasks", AiAssistantContextContract.TaskBreakdownCapability)]
    [InlineData("Ta\u0309o\t10\nTask cho Sprint 1", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Tạo task để chạy test CAND", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Tạo 3 task để theo dõi Project hiện tại", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Tạo 3 task để cập nhật roadmap của Project", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Khởi tạo Project mới; sau đó soạn checklist nghiệm thu", AiAssistantContextContract.ProjectLaunchCapability)]
    [InlineData("Tạo 4 task con cho Task đang mở", AiAssistantContextContract.TaskBreakdownCapability)]
    [InlineData("Lập phương án manager/team rồi tạo Task", AiAssistantContextContract.ProjectStaffingPlanCapability)]
    [InlineData("Nhờ AI soạn 3 task cho Sprint 1", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Tôi muốn trợ lý AI tạo Project mới", AiAssistantContextContract.ProjectLaunchCapability)]
    public void AffirmativeDrafts_PreserveExactActionDespiteContextOrNoWriteGuard(string prompt, string expected)
        => AiAssistantCapabilityIntentClassifier.Infer(prompt).Should().Be(expected);

    [Theory]
    [InlineData("OK, phân tích tiến độ Project hiện tại", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Tóm tắt wiki này", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Không tiếp tục tạo Project", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Tiếp tục soạn 5 mục checklist nghiệm thu", AiAssistantContextContract.AcceptanceChecklistCapability)]
    [InlineData("Booking còn bao nhiêu task?", AiAssistantContextContract.GroundedReadCapability)]
    public void CurrentExplicitRequest_WinsOverOldTopic(string prompt, string expected)
    {
        AiChatMessageDto[] history =
        [
            new("user", "Khởi tạo Project web SPA"),
            new("assistant", "Mình đã soạn Launch Brief, chưa tạo Project.")
        ];
        AiAssistantCapabilityIntentClassifier.Infer(prompt, history).Should().Be(expected);
    }

    [Fact]
    public void Continuation_DoesNotTreatAssistantSuggestionsAsUserIntent()
    {
        AiChatMessageDto[] history =
        [
            new("user", "Tóm tắt tiến độ Project"),
            new("assistant", "Bạn có thể tạo Project Launch, soạn checklist hoặc tạo poll tiếp theo.")
        ];
        AiAssistantCapabilityIntentClassifier.Infer("tiếp tục", history)
            .Should().Be(AiAssistantContextContract.GroundedReadCapability);
    }

    [Fact]
    public void RepeatedContinuation_PreservesLatestUserActionWithoutReadingAssistantSuggestions()
    {
        AiChatMessageDto[] history =
        [
            new("user", "Soạn Poll trong Group đang mở"),
            new("assistant", "Đây là bản nháp Poll. Bạn cũng có thể tạo Project."),
            new("user", "tiếp tục"),
            new("assistant", "Bạn có thể soạn checklist nếu cần."),
            new("user", "ok"),
            new("assistant", "Chờ xác nhận trong card.")
        ];
        AiAssistantCapabilityIntentClassifier.Infer("tiếp tục", history).Should().Be(AiAssistantContextContract.GroupPollCapability);
    }

    [Theory]
    [InlineData("Tạo 111 task", "tao 111 task")]
    [InlineData("Booking", "booking")]
    [InlineData("Dừng weekly digest", "ngung weekly digest")]
    [InlineData("Dùng weekly digest", "dung weekly digest")]
    public void Normalization_PreservesNumbersAndMeaning(string prompt, string normalized)
        => AiPromptLanguage.Normalize(prompt).Should().Be(normalized);

    [Fact]
    public void LegacyConversationTools_AllowOnlyKnownReadsEvenForFullRole()
    {
        string[] names = ["GetProjectSummary", "GetOverdueTasks", "GetMyTimeLogs", "SearchKnowledge",
            "GetMemberWorkload", "SuggestTaskAssignment", "CreateTask", "UpdateTaskStatus", "AssignTask",
            "AddDueDate", "SetTaskPriority", "AddComment", "StartTimeTracking", "StopTimeTracking", "FutureUnknownTool"];
        var tools = names.Select(name => (AITool)AIFunctionFactory.Create(() => "unused", name: name)).ToList();
        var filtered = ErumiChatService.ReadOnlyConversationTools(tools)!;
        filtered.OfType<AIFunction>().Select(tool => tool.Name).Should().BeEquivalentTo(names.Take(6));
        ErumiChatService.ReadOnlyConversationTools(null).Should().BeNull();
    }
}
