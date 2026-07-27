using System.Text.Json;
using System.Globalization;
using FluentAssertions;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class ProgressSummaryContractTests
{
    [Fact]
    public void TryBuildResult_GroundedProviderFragment_MergesServerOwnedFacts()
    {
        var snapshot = Snapshot(total: 4, done: 2, inProgress: 1, todo: 1);
        const string provider = """
            {
              "summaryPoints":[
                {
                  "text":"Hai trong bốn nhiệm vụ đã hoàn thành.",
                  "metricRefs":["done","total"],
                  "sourceRefs":["project:11111111-1111-1111-1111-111111111111"]
                }
              ],
              "risks":[
                {
                  "code":"OVERDUE_TASK",
                  "severity":"high",
                  "title":"Một nhiệm vụ đang quá hạn.",
                  "metricRefs":["overdue"],
                  "sourceRefs":["task:22222222-2222-2222-2222-222222222222"]
                }
              ],
              "nextActions":[
                {
                  "title":"Rà soát nhiệm vụ quá hạn",
                  "rationale":"Ưu tiên xử lý điểm nghẽn đã có nguồn.",
                  "metricRefs":["overdue"],
                  "sourceRefs":["task:22222222-2222-2222-2222-222222222222"]
                }
              ]
            }
            """;

        var valid = ProgressSummaryContract.TryBuildResult(
            provider,
            snapshot,
            out var resultJson,
            out var error);

        valid.Should().BeTrue(error);
        using var result = JsonDocument.Parse(resultJson);
        result.RootElement.GetProperty("metrics").GetProperty("total").GetInt32().Should().Be(4);
        result.RootElement.GetProperty("metrics").GetProperty("completionRate").GetDecimal().Should().Be(50m);
        result.RootElement.GetProperty("summaryPoints")[0].GetProperty("text").GetString()
            .Should().Be("Hai trong bốn nhiệm vụ đã hoàn thành.");
        result.RootElement.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "scope", "period", "coverage", "metrics", "summaryPoints", "risks", "nextActions", "sourceRefs", "warnings");
    }

    [Fact]
    public void TryBuildResult_UnknownGroundingReference_FailsClosed()
    {
        const string provider = """
            {
              "summaryPoints":[
                {
                  "text":"A factual statement with an invented reference.",
                  "metricRefs":["velocity"],
                  "sourceRefs":["task:ffffffff-ffff-ffff-ffff-ffffffffffff"]
                }
              ],
              "risks":[],
              "nextActions":[]
            }
            """;

        var valid = ProgressSummaryContract.TryBuildResult(
            provider,
            Snapshot(total: 1, done: 0, inProgress: 1, todo: 0),
            out _,
            out var error);

        valid.Should().BeFalse();
        error.Should().Contain("grounding reference");
    }

    [Fact]
    public void TryBuildEmptyResult_ProducesValidatedZeroFactResultWithoutNarrative()
    {
        var valid = ProgressSummaryContract.TryBuildEmptyResult(
            Snapshot(total: 0, done: 0, inProgress: 0, todo: 0),
            out var resultJson,
            out var error);

        valid.Should().BeTrue(error);
        using var result = JsonDocument.Parse(resultJson);
        result.RootElement.GetProperty("summaryPoints").GetArrayLength().Should().Be(0);
        result.RootElement.GetProperty("risks").GetArrayLength().Should().Be(0);
        result.RootElement.GetProperty("nextActions").GetArrayLength().Should().Be(0);
        result.RootElement.GetProperty("warnings")[0].GetString().Should().Be("NO_PROGRESS_TASKS");
    }

    [Fact]
    public void TryBuildResult_SprintScope_PreservesSprintIdentityAndGrounding()
    {
        var sprintId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var provider = $$"""
            {
              "summaryPoints":[{
                "text":"Mốc có một task đang thực hiện.",
                "metricRefs":["inProgress"],
                "sourceRefs":["sprint:{{sprintId:D}}"]
              }],
              "risks":[],
              "nextActions":[]
            }
            """;

        ProgressSummaryContract.TryBuildResult(
            provider,
            SprintSnapshot(sprintId, total: 1, done: 0, inProgress: 1, todo: 0),
            out var resultJson,
            out var error).Should().BeTrue(error);
        using var result = JsonDocument.Parse(resultJson);
        result.RootElement.GetProperty("scope").GetProperty("type").GetString().Should().Be("sprint");
        result.RootElement.GetProperty("scope").GetProperty("sprintId").GetGuid().Should().Be(sprintId);
        result.RootElement.GetProperty("sourceRefs")[0].GetProperty("type").GetString().Should().Be("sprint");
    }

    [Fact]
    public void TryValidateFinal_ExtraPropertyOrInconsistentMetric_FailsClosed()
    {
        var invalid = """
            {
              "scope":{"projectId":"11111111-1111-1111-1111-111111111111","projectName":"Alpha","projectCode":"ALP"},
              "period":{"kind":"current_snapshot","snapshotAt":"2026-07-27T10:00:00Z"},
              "coverage":{"dataState":"sufficient","visibility":"manager_full_project","includedTaskCount":4,"excludedTaskCount":0},
              "metrics":{"total":4,"done":2,"inProgress":2,"todo":1,"overdue":1,"dueSoon":0,"completionRate":50},
              "summaryPoints":[],
              "risks":[],
              "nextActions":[],
              "sourceRefs":[{"key":"project:11111111-1111-1111-1111-111111111111","type":"project","entityId":"11111111-1111-1111-1111-111111111111","label":"Alpha","url":"/projects/11111111-1111-1111-1111-111111111111","version":"v1"}],
              "warnings":[],
              "unexpected":true
            }
            """;

        ProgressSummaryContract.TryValidateFinal(invalid, out var error).Should().BeFalse();
        error.Should().NotBeNullOrWhiteSpace();
    }

    private static string Snapshot(int total, int done, int inProgress, int todo)
    {
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var taskId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        return JsonSerializer.Serialize(new
        {
            snapshotVersion = "project_progress_snapshot.v1",
            scope = new { projectId, projectName = "Alpha", projectCode = "ALP" },
            period = new
            {
                kind = "current_snapshot",
                snapshotAt = DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)
            },
            coverage = new
            {
                dataState = total == 0 ? "empty" : "sufficient",
                visibility = "manager_full_project",
                includedTaskCount = total,
                excludedTaskCount = 0
            },
            metrics = new
            {
                total,
                done,
                inProgress,
                todo,
                overdue = total == 0 ? 0 : 1,
                dueSoon = 0,
                completionRate = total == 0 ? 0m : Math.Round(done * 100m / total, 2)
            },
            sourceRefs = new object[]
            {
                new
                {
                    key = $"project:{projectId:D}",
                    type = "project",
                    entityId = projectId,
                    label = "Alpha",
                    url = $"/projects/{projectId:D}",
                    version = "v1"
                },
                new
                {
                    key = $"task:{taskId:D}",
                    type = "task",
                    entityId = taskId,
                    label = "Risk task",
                    url = $"/projects/{projectId:D}/tasks/{taskId:D}",
                    version = "v1"
                }
            },
            taskFacts = Array.Empty<object>()
        });
    }

    private static string SprintSnapshot(Guid sprintId, int total, int done, int inProgress, int todo)
    {
        var projectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        return JsonSerializer.Serialize(new
        {
            snapshotVersion = "sprint_progress_snapshot.v1",
            scope = new
            {
                type = "sprint",
                projectId,
                projectName = "Alpha",
                projectCode = "ALP",
                sprintId,
                sprintName = "Release milestone"
            },
            period = new
            {
                kind = "current_snapshot",
                snapshotAt = DateTimeOffset.Parse("2026-07-27T10:00:00Z", CultureInfo.InvariantCulture)
            },
            coverage = new
            {
                dataState = total == 0 ? "empty" : "sufficient",
                visibility = "manager_full_project",
                includedTaskCount = total,
                excludedTaskCount = 0
            },
            metrics = new
            {
                total,
                done,
                inProgress,
                todo,
                overdue = 0,
                dueSoon = 0,
                completionRate = total == 0 ? 0m : Math.Round(done * 100m / total, 2)
            },
            sourceRefs = new object[]
            {
                new
                {
                    key = $"sprint:{sprintId:D}",
                    type = "sprint",
                    entityId = sprintId,
                    label = "Release milestone",
                    url = $"/projects/{projectId:D}#milestone-{sprintId:D}",
                    version = "v1"
                }
            },
            taskFacts = Array.Empty<object>()
        });
    }
}
