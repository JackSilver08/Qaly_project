using System.Text.Json;
using FluentAssertions;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiNativeCandidateContractTests
{
    [Fact]
    public void GroupSummary_OnlyAcceptsAuthorizedOrderedMessageReferences()
    {
        var groupId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var key = $"message:{messageId:D}";
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaId = GroupSummaryAiContract.SnapshotSchemaId,
            groupId,
            projectId,
            messageIds = new[] { messageId },
            sourceRefs = new[] { new { key, messageId, url = $"/groups/{groupId:D}?messageId={messageId:D}" } }
        });
        var provider = JsonSerializer.Serialize(new
        {
            summary = "Đã thống nhất phạm vi.",
            summarySourceRefs = new[] { key },
            keyDecisions = new[] { new { text = "Chốt phạm vi.", sourceRefs = new[] { key } } },
            openQuestions = Array.Empty<object>(),
            actionCandidates = Array.Empty<object>()
        });

        GroupSummaryOutputContract.TryBuildResult(provider, snapshot, out var result, out var error).Should().BeTrue(error);
        using var document = JsonDocument.Parse(result);
        document.RootElement.GetProperty("messageRange").GetProperty("messageIds")[0].GetGuid().Should().Be(messageId);

        var forged = provider.Replace(key, $"message:{Guid.NewGuid():D}", StringComparison.Ordinal);
        GroupSummaryOutputContract.TryBuildResult(forged, snapshot, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void MeetingChecknote_RejectsEvidenceThatIsNotInTranscript()
    {
        const string transcript = "[09:00] An: Hoàn thành API trước thứ sáu.";
        var valid = JsonSerializer.Serialize(new
        {
            summary = "Nhóm cần hoàn thành API.",
            summaryEvidence = new[] { transcript },
            decisions = Array.Empty<object>(),
            risks = Array.Empty<object>(),
            actionItems = new[]
            {
                new { title = "Hoàn thành API", description = (string?)null, suggestedOwner = "An", dueDate = (string?)null, priority = "High", evidence = "Hoàn thành API trước thứ sáu." }
            }
        });

        MeetingChecknoteAiContract.TryValidateModel(valid, transcript, out var output, out var error).Should().BeTrue(error);
        output!.ActionItems.Should().HaveCount(1);

        var forged = valid.Replace("Hoàn thành API trước thứ sáu.", "Một câu không tồn tại", StringComparison.Ordinal);
        var forgedRoot = System.Text.Json.Nodes.JsonNode.Parse(valid)!.AsObject();
        forgedRoot["summaryEvidence"] = new System.Text.Json.Nodes.JsonArray("evidence that is absent from the transcript");
        forged = forgedRoot.ToJsonString();
        MeetingChecknoteAiContract.TryValidateModel(forged, transcript, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void DashboardBrief_PreservesServerMetricsAndRejectsUnknownMetricReferences()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaId = DashboardStrategicBriefAiContract.SnapshotSchemaId,
            organizationId,
            requestedById = userId,
            snapshotAt = DateTimeOffset.UtcNow,
            coverage = new { visibleProjectCount = 1, includedTaskCount = 2, excludedPrivateTaskCount = 1, visibility = "authorized_tenant_non_private" },
            metrics = new { projectCount = 1, activeProjectCount = 1, riskProjectCount = 1, taskTotal = 2, done = 1, inProgress = 1, todo = 0, overdue = 1, dueSoon = 0, completionRate = 50m, memberCount = 2 },
            sourceRefs = new[] { new { key = $"project:{projectId:D}", type = "project", entityId = projectId, projectId, label = "Project", url = $"/projects/{projectId:D}", version = "v1" } }
        });
        var provider = JsonSerializer.Serialize(new
        {
            summaryPoints = new[] { new { text = "Một trong hai task đã hoàn thành.", metricRefs = new[] { "completionRate" }, sourceRefs = Array.Empty<string>() } },
            risks = new[] { new { severity = "high", title = "Có task quá hạn.", metricRefs = new[] { "overdue" }, sourceRefs = Array.Empty<string>() } },
            priorities = Array.Empty<object>()
        });

        DashboardStrategicBriefOutputContract.TryBuildResult(provider, snapshot, out var result, out var error).Should().BeTrue(error);
        using var document = JsonDocument.Parse(result);
        document.RootElement.GetProperty("metrics").GetProperty("completionRate").GetDecimal().Should().Be(50m);

        var forged = provider.Replace("completionRate", "madeUpMetric", StringComparison.Ordinal);
        DashboardStrategicBriefOutputContract.TryBuildResult(forged, snapshot, out _, out _).Should().BeFalse();
    }
}
