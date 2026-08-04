using System.Text.Json;
using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class TaskDraftAiContractTests
{
    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GroupId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid MessageId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid MemberId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    [Trait("TestId", "TEST-TASK-DRAFT-01")]
    public void ValidProviderResult_IsNormalizedAndKeepsAuthorizedGrounding()
    {
        var provider = $$"""
        {
          "schemaId": "task_draft.v5",
          "dataState": "ready",
          "tasks": [{
            "clientId": "one",
            "title": "  Hoàn thiện API đăng nhập  ",
            "description": "Kèm tiêu chí nghiệm thu.",
            "priority": "high",
            "status": "Todo",
            "dueDate": null,
            "assigneeId": "{{MemberId}}",
            "selected": true,
            "confidence": 0.82,
            "sourceRefs": ["message:{{MessageId}}"]
          }]
        }
        """;

        TaskDraftAiContract.TryBuildResult(provider, Snapshot(), out var normalized, out var error)
            .Should().BeTrue(error);
        var payload = JsonSerializer.Deserialize<AiTaskDraftPayload>(normalized, JsonOptions())!;
        payload.SchemaId.Should().Be(TaskDraftAiContract.SchemaId);
        payload.Tasks.Should().ContainSingle();
        payload.Tasks[0].Title.Should().Be("Hoàn thiện API đăng nhập");
        payload.Tasks[0].Priority.Should().Be("High");
        payload.Tasks[0].SourceRefs.Should().Equal($"message:{MessageId:D}");

        new AiOutputValidator().Validate(provider, TaskDraftAiContract.SchemaId, Snapshot(), out error)
            .Should().BeTrue(error);
    }

    [Theory]
    [InlineData("invented-source", false)]
    [InlineData("invented-member", false)]
    [InlineData("no-source", false)]
    [Trait("TestId", "TEST-TASK-DRAFT-02")]
    public void InventedReferencesAndMembers_FailClosed(string variant, bool expected)
    {
        var sourceRefs = variant == "no-source"
            ? "[]"
            : variant == "invented-source"
                ? "[\"message:99999999-9999-9999-9999-999999999999\"]"
                : $"[\"message:{MessageId:D}\"]";
        var assignee = variant == "invented-member"
            ? "\"99999999-9999-9999-9999-999999999999\""
            : "null";
        var provider = $$"""
        {"schemaId":"task_draft.v5","dataState":"ready","tasks":[{
          "clientId":"one","title":"Create grounded task","description":null,
          "priority":"Medium","status":"Todo","dueDate":null,"assigneeId":{{assignee}},
          "selected":true,"confidence":0.7,"sourceRefs":{{sourceRefs}}
        }]}
        """;

        TaskDraftAiContract.TryBuildResult(provider, Snapshot(), out _, out _).Should().Be(expected);
    }

    [Fact]
    [Trait("TestId", "TEST-TASK-DRAFT-03")]
    public void InsufficientEvidence_IsAnHonestEmptyState()
    {
        const string empty = """{"schemaId":"task_draft.v5","dataState":"insufficient_evidence","tasks":[]}""";
        TaskDraftAiContract.TryBuildResult(empty, Snapshot(), out var normalized, out var error)
            .Should().BeTrue(error);
        normalized.Should().Contain("insufficient_evidence");

        const string dishonest = """{"schemaId":"task_draft.v5","dataState":"insufficient_evidence","tasks":[{"title":"Fake"}]}""";
        TaskDraftAiContract.TryBuildResult(dishonest, Snapshot(), out _, out _).Should().BeFalse();
    }

    private static string Snapshot()
        => JsonSerializer.Serialize(new TaskDraftSourceSnapshotDto(
            TaskDraftAiContract.SnapshotSchemaId,
            ProjectId,
            GroupId,
            "ready",
            [new TaskDraftAuthorizedSourceDto(
                $"message:{MessageId:D}",
                MessageId,
                $"/groups/{GroupId:D}?messageId={MessageId:D}",
                "v1",
                "hash")],
            [new TaskDraftAuthorizedMemberDto(MemberId, "Authorized Member")]), JsonOptions());

    private static JsonSerializerOptions JsonOptions()
        => new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
}
