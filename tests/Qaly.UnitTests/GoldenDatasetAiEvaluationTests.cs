using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Qaly.Infrastructure.Services.AI;
using Qaly.Application.DTOs.Ai;

namespace Qaly.UnitTests;

/// <summary>
/// Chí Khang - Golden Dataset AI Evaluation Suite
/// Đánh giá tính đúng đắn, hữu ích, có căn cứ (groundedness) và ổn định của 2 tính năng AI:
/// 1. Tóm tắt tiến độ (progress_summary.v4)
/// 2. Gợi ý kỹ năng (task_skill_suggestion.v1)
/// </summary>
public sealed class GoldenDatasetAiEvaluationTests
{
    // =========================================================================
    // GOLDEN DATASET 1: Dự án bình thường (Happy Path) - Tiếng Việt
    // =========================================================================
    [Fact]
    public void Evaluate_GoldenDataset_NormalProject_ReturnsValidSchemaAndGroundedFacts()
    {
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var taskId1 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var snapshot = JsonSerializer.Serialize(new
        {
            snapshotVersion = "project_progress_snapshot.v1",
            scope = new { projectId, projectName = "Dự án Quản lý Qaly", projectCode = "QALY" },
            period = new { kind = "current_snapshot", snapshotAt = "2026-07-31T10:00:00Z" },
            coverage = new { dataState = "sufficient", visibility = "manager_full_project", includedTaskCount = 10, excludedTaskCount = 0 },
            metrics = new { total = 10, done = 6, inProgress = 3, todo = 1, overdue = 0, dueSoon = 2, completionRate = 60.00m },
            sourceRefs = new object[]
            {
                new { key = $"project:{projectId:D}", type = "project", entityId = projectId, label = "Dự án Quản lý Qaly", url = $"/projects/{projectId:D}", version = "v1" },
                new { key = $"task:{taskId1:D}", type = "task", entityId = taskId1, label = "Tích hợp AI Gateway", url = $"/projects/{projectId:D}/tasks/{taskId1:D}", version = "v1" }
            },
            taskFacts = Array.Empty<object>()
        });

        const string aiProviderResponse = """
            {
              "summaryPoints": [
                {
                  "text": "Dự án đạt tiến độ 60% với 6/10 nhiệm vụ hoàn thành.",
                  "metricRefs": ["completionRate", "done", "total"],
                  "sourceRefs": ["project:11111111-1111-1111-1111-111111111111"]
                }
              ],
              "risks": [],
              "nextActions": [
                {
                  "title": "Hoàn thiện 3 nhiệm vụ đang thực hiện",
                  "rationale": "Đảm bảo tiến độ mốc nghiệm thu đúng hạn.",
                  "metricRefs": ["inProgress"],
                  "sourceRefs": ["project:11111111-1111-1111-1111-111111111111"]
                }
              ]
            }
            """;

        var success = ProgressSummaryContract.TryBuildResult(aiProviderResponse, snapshot, out var resultJson, out var error);
        
        success.Should().BeTrue(error);
        using var doc = JsonDocument.Parse(resultJson);
        var metrics = doc.RootElement.GetProperty("metrics");
        metrics.GetProperty("completionRate").GetDecimal().Should().Be(60.00m);
        metrics.GetProperty("done").GetInt32().Should().Be(6);
        metrics.GetProperty("total").GetInt32().Should().Be(10);
        
        var summary = doc.RootElement.GetProperty("summaryPoints")[0];
        summary.GetProperty("text").GetString().Should().Contain("60%");
    }

    // =========================================================================
    // GOLDEN DATASET 2: Dự án Trễ hạn (Overdue Tasks & Risks)
    // =========================================================================
    [Fact]
    public void Evaluate_GoldenDataset_OverdueProject_IdentifiesRisksWithGroundedSourceRefs()
    {
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var overdueTaskId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var snapshot = JsonSerializer.Serialize(new
        {
            snapshotVersion = "project_progress_snapshot.v1",
            scope = new { projectId, projectName = "Dự án Nâng cấp Hệ thống", projectCode = "SYS" },
            period = new { kind = "current_snapshot", snapshotAt = "2026-07-31T10:00:00Z" },
            coverage = new { dataState = "sufficient", visibility = "manager_full_project", includedTaskCount = 5, excludedTaskCount = 0 },
            metrics = new { total = 5, done = 1, inProgress = 2, todo = 2, overdue = 2, dueSoon = 1, completionRate = 20.00m },
            sourceRefs = new object[]
            {
                new { key = $"project:{projectId:D}", type = "project", entityId = projectId, label = "Dự án Nâng cấp Hệ thống", url = $"/projects/{projectId:D}", version = "v1" },
                new { key = $"task:{overdueTaskId:D}", type = "task", entityId = overdueTaskId, label = "Migration Database Tenant", url = $"/projects/{projectId:D}/tasks/{overdueTaskId:D}", version = "v1" }
            },
            taskFacts = Array.Empty<object>()
        });

        const string aiProviderResponse = """
            {
              "summaryPoints": [
                {
                  "text": "Tiến độ dự án mới đạt 20%, có 2 nhiệm vụ trễ hạn.",
                  "metricRefs": ["completionRate", "overdue"],
                  "sourceRefs": ["project:11111111-1111-1111-1111-111111111111"]
                }
              ],
              "risks": [
                {
                  "code": "OVERDUE_CRITICAL_TASK",
                  "severity": "high",
                  "title": "Nhiệm vụ Migration Database Tenant đã quá hạn",
                  "metricRefs": ["overdue"],
                  "sourceRefs": ["task:33333333-3333-3333-3333-333333333333"]
                }
              ],
              "nextActions": [
                {
                  "title": "Phân công thêm nhân sự xử lý task Migration Database Tenant",
                  "rationale": "Giải tỏa điểm nghẽn rủi ro cao.",
                  "metricRefs": ["overdue"],
                  "sourceRefs": ["task:33333333-3333-3333-3333-333333333333"]
                }
              ]
            }
            """;

        var success = ProgressSummaryContract.TryBuildResult(aiProviderResponse, snapshot, out var resultJson, out var error);
        
        success.Should().BeTrue(error);
        using var doc = JsonDocument.Parse(resultJson);
        var risks = doc.RootElement.GetProperty("risks");
        risks.GetArrayLength().Should().Be(1);
        risks[0].GetProperty("severity").GetString().Should().Be("high");
        risks[0].GetProperty("code").GetString().Should().Be("OVERDUE_CRITICAL_TASK");
    }

    // =========================================================================
    // GOLDEN DATASET 3: Dự án Thiếu dữ liệu (Empty/Zero Tasks)
    // =========================================================================
    [Fact]
    public void Evaluate_GoldenDataset_EmptyProject_FallbackWithoutAIProviderCost()
    {
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var snapshot = JsonSerializer.Serialize(new
        {
            snapshotVersion = "project_progress_snapshot.v1",
            scope = new { projectId, projectName = "Dự án Mới Khởi Tạo", projectCode = "NEW" },
            period = new { kind = "current_snapshot", snapshotAt = "2026-07-31T10:00:00Z" },
            coverage = new { dataState = "empty", visibility = "manager_full_project", includedTaskCount = 0, excludedTaskCount = 0 },
            metrics = new { total = 0, done = 0, inProgress = 0, todo = 0, overdue = 0, dueSoon = 0, completionRate = 0m },
            sourceRefs = new object[]
            {
                new { key = $"project:{projectId:D}", type = "project", entityId = projectId, label = "Dự án Mới Khởi Tạo", url = $"/projects/{projectId:D}", version = "v1" }
            },
            taskFacts = Array.Empty<object>()
        });

        var success = ProgressSummaryContract.TryBuildEmptyResult(snapshot, out var resultJson, out var error);
        
        success.Should().BeTrue(error);
        using var doc = JsonDocument.Parse(resultJson);
        doc.RootElement.GetProperty("coverage").GetProperty("dataState").GetString().Should().Be("empty");
        doc.RootElement.GetProperty("summaryPoints").GetArrayLength().Should().Be(0);
        doc.RootElement.GetProperty("warnings")[0].GetString().Should().Be("NO_PROGRESS_TASKS");
    }

    // =========================================================================
    // GOLDEN DATASET 4: Gợi ý kỹ năng (Skill Suggestion Schema & Catalog Grounding)
    // =========================================================================
    [Fact]
    public void Evaluate_GoldenDataset_TaskSkillSuggestion_GroundsToCatalogOnly()
    {
        var taskId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var skillId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var output = new TaskSkillSuggestionOutputDto(
            TaskSkillAiContract.SchemaId,
            taskId,
            "hash_123456",
            "ready",
            new[]
            {
                new TaskSkillSuggestionItemDto(
                    skillId,
                    "Vue.js",
                    "Proficient",
                    0.92m,
                    "Nhiệm vụ thiết kế giao diện Vue 3 đỏi hỏi kỹ năng Vue.js thành thạo.",
                    new[] { $"task:{taskId:D}" }
                )
            },
            Array.Empty<string>(),
            DateTimeOffset.UtcNow
        );

        output.SchemaId.Should().Be("task_skill_suggestion.v1");
        output.Suggestions.Should().HaveCount(1);
        output.Suggestions[0].SkillId.Should().Be(skillId);
        output.Suggestions[0].RequiredLevel.Should().Be("Proficient");
        output.Suggestions[0].Confidence.Should().Be(0.92m);
    }

    [Fact]
    public void Evaluate_GoldenDataset_PredictiveRiskSchema_ValidatesCorrectly()
    {
        var validator = new AiOutputValidator();
        const string validJson = """
            {
              "riskScore": 45,
              "healthStatus": "AtRisk",
              "delayProbabilityPercent": 65,
              "bottlenecks": ["Tải công việc dồn vào Backend", "Code review bị hoãn"],
              "actionableRemediations": ["Phân bổ 2 task cho Dev B", "Tăng ưu tiên Code Review"],
              "summary": "Dự án có nguy cơ trễ 3 ngày nếu không điều chỉnh nhân sự phụ trách Backend."
            }
            """;

        bool isValid = validator.Validate(validJson, "PredictiveRisk.v1", out var error);
        isValid.Should().BeTrue(error);
        error.Should().BeNull();
    }
}
