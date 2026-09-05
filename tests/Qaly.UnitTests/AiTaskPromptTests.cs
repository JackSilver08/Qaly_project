using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;

namespace Qaly.UnitTests;

public sealed class AiTaskPromptTests
{
    [Theory]
    [InlineData("Tạo 3 task: đăng nhập, thanh toán, kiểm thử; giao cho Hoàng Tuấn Kiệt", "đăng nhập, thanh toán, kiểm thử", "Hoàng Tuấn Kiệt")]
    [InlineData("Tạo task sửa form đăng nhập và gán cho người Hoàng Tuấn Kiệt", "sửa form đăng nhập", "Hoàng Tuấn Kiệt")]
    [InlineData("Tao 1 task sua login, giao cho Kiet; mo ban nhap", "sua login", "Kiet")]
    [InlineData("Tạo task \"Sửa đăng nhập\" giao cho \"Hoàng Tuấn Kiệt\"", "\"Sửa đăng nhập\"", "Hoàng Tuấn Kiệt")]
    [InlineData("Create 1 task fix login and assign to Alice; show a draft", "fix login", "Alice")]
    [InlineData("Tạo 1 task cho người Kiệt: sửa đăng nhập", "sửa đăng nhập", "Kiệt")]
    [InlineData("Tạo 1 task sửa đăng nhập 🔐; giao cho Kiệt", "sửa đăng nhập 🔐", "Kiệt")]
    [InlineData("Tạo task thiết kế giao diện cho khách hàng và gán cho Kiệt", "thiết kế giao diện cho khách hàng", "Kiệt")]
    [InlineData("Tạo task thiết kế giao diện cho người dùng và gán cho Kiệt", "thiết kế giao diện cho người dùng", "Kiệt")]
    public void SeparatesContentAndRecipient(string message, string content, string recipient)
    {
        var parsed = AiTaskPrompt.Parse(message);
        parsed.Content.Should().Be(content);
        parsed.Assignee.Should().Be(recipient);
        parsed.ValidateAssignmentContent().Should().BeNull();
    }

    [Theory]
    [InlineData("Tạo 3 task gán cho người Kiệt")]
    [InlineData("Tạo task giao task cho người xx")]
    [InlineData("Tạo 3 task cho Sprint 1; giao cho Kiệt")]
    public void MissingDeliverableRequiresClarification(string message)
        => AiTaskPrompt.Parse(message).ValidateAssignmentContent().Should().NotBeNull();

    [Fact]
    public void QuotedBusinessTitleIsNotAssignmentInstruction()
    {
        var parsed = AiTaskPrompt.Parse("Tạo 1 task tên \"Giao task cho người mới trong nhóm\"");
        parsed.Assignee.Should().BeNull();
        parsed.TaskTitles(1).Should().Equal("Giao task cho người mới trong nhóm");
    }

    [Theory]
    [InlineData("Tạo 1 task sửa login; không giao cho Kiệt")]
    [InlineData("Tạo 1 task sửa login; để chưa giao")]
    [InlineData("Tạo 1 task sửa login; giữ chưa giao")]
    public void ExplicitUnassignedIsNotAnAssigneeRequest(string message)
    {
        var parsed = AiTaskPrompt.Parse(message);
        parsed.Assignee.Should().BeNull();
        parsed.LeaveUnassigned.Should().BeTrue();
        parsed.TaskTitles(1).Should().Equal("sửa login");
    }

    [Fact]
    public void MultipleAssignmentInstructionsAreNotSilentlyCollapsed()
        => AiTaskPrompt.Parse("Tạo 2 task: login giao cho An; payment giao cho Bình").Error.Should().NotBeNull();

    [Fact]
    public void ResolveUsesAuthorizedMembersAndDoesNotGuessDuplicateNames()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        (Guid Id, string Name)[] members = [(first, "Hoàng Tuấn Kiệt"), (second, "Nguyễn Minh Kiệt")];
        AiTaskPrompt.ResolveAssignee("Hoang Tuan Kiet", members).Id.Should().Be(first);
        AiTaskPrompt.ResolveAssignee("Kiệt", members).Error.Should().NotBeNull();
        AiTaskPrompt.ResolveAssignee("xx", members).Error.Should().NotBeNull();
        AiTaskPrompt.ResolveAssignee("iet", members).Error.Should().NotBeNull();
    }

    [Theory]
    [InlineData("Giao 3 task cho người Kiệt", AiAssistantContextContract.TaskAssignmentScheduleCapability)]
    [InlineData("Gán 3 task cho người Kiệt", AiAssistantContextContract.TaskAssignmentScheduleCapability)]
    [InlineData("Kiểm tra giao diện Task đang mở", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Phân tích các task gần deadline", AiAssistantContextContract.GroundedReadCapability)]
    [InlineData("Tạo 3 task và giao cho người Kiệt", AiAssistantContextContract.TaskCreateCapability)]
    [InlineData("Tạo task giao task cho người xx", AiAssistantContextContract.TaskCreateCapability)]
    public void ExistingAssignmentAndCreateThenAssignRemainDifferentRoutes(string prompt, string capability)
        => AiAssistantCapabilityIntentClassifier.Infer(prompt).Should().Be(capability);
}
