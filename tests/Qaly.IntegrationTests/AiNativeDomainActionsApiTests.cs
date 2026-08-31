using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.DTOs.Meeting;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.IntegrationTests;

public sealed class AiNativeDomainActionsApiTests : IClassFixture<IntegrationTestFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AiNativeDomainActionsApiTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-P16-P17-CONVERSATION-ROUTE-01")]
    public async Task P16P17_NaturalTaskPromptsWithoutRequestedCapability_ReturnTypedReviewCards()
    {
        var seeded = await SeedAsync();
        var context = new AiAssistantClientContextDto(
            $"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", "task",
            seeded.ProjectId, "task", seeded.TaskId);

        var checklist = await PrepareFromNaturalPromptAsync(
            AiNativeDomainActionContract.ChecklistCapability,
            "Với Task đang mở, soạn 5 mục acceptance checklist kiểm chứng được, cho phép sửa từng mục và chờ một xác nhận trước khi lưu.",
            context);
        checklist.Payload.GetProperty("items").GetArrayLength().Should().Be(5);

        var breakdown = await PrepareFromNaturalPromptAsync(
            AiNativeDomainActionContract.BreakdownCapability,
            "Tách Task đang mở thành 4 subtask theo thứ tự thực hiện, có dependency, estimate và required skill; mở card review trước khi tạo.",
            context);
        breakdown.Payload.GetProperty("subtasks").GetArrayLength().Should().Be(4);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskAcceptanceChecklistItems.CountAsync(item => item.TaskId == seeded.TaskId)).Should().Be(0);
        (await db.TaskItems.CountAsync(item => item.ParentTaskId == seeded.TaskId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-DOMAIN-01")]
    public async Task P16P17_ChecklistAndFourSubtasks_ConfirmCanonicalRowsSkillsDependenciesReadBackAndReplay()
    {
        var seeded = await SeedAsync();
        var checklist = await PrepareAsync(
            AiNativeDomainActionContract.ChecklistCapability,
            "Với Task đang mở, soạn 5 mục acceptance checklist kiểm chứng được, cho phép sửa từng mục và chờ một xác nhận trước khi lưu.",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", "task",
                seeded.ProjectId, "task", seeded.TaskId));
        checklist.Status.Should().Be("pending_review");
        checklist.Payload.GetProperty("items").GetArrayLength().Should().Be(5);

        var checklistKey = $"checklist-{Guid.NewGuid():N}";
        var checklistReceipt = await ConfirmAsync(checklist, checklistKey);
        checklistReceipt.Items.Should().NotBeEmpty();
        checklistReceipt.ReadBackLinks.Should().OnlyContain(link =>
            link.EndsWith($"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", StringComparison.Ordinal));
        var checklistReplay = await ConfirmAsync(checklist, checklistKey);
        checklistReplay.Replayed.Should().BeTrue();
        checklistReplay.ReceiptId.Should().Be(checklistReceipt.ReceiptId);
        var wrongLogicalKey = await ConfirmResponseAsync(checklist, $"checklist-other-{Guid.NewGuid():N}");
        wrongLogicalKey.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var checklistRows = await _client.GetFromJsonAsync<List<TaskAcceptanceChecklistItemDto>>(
            $"/api/ai/native-actions/tasks/{seeded.TaskId}/acceptance-checklist", JsonOptions);
        checklistRows.Should().HaveCount(5);
        var firstChecklistRow = checklistRows![0];
        var toggleResponse = await SendAsync(
            HttpMethod.Patch,
            $"/api/ai/native-actions/acceptance-checklist/{firstChecklistRow.Id}",
            new UpdateTaskAcceptanceChecklistItemRequestDto(true, firstChecklistRow.RowVersion),
            await CsrfAsync());
        toggleResponse.StatusCode.Should().Be(HttpStatusCode.OK, await toggleResponse.Content.ReadAsStringAsync());
        var toggled = (await toggleResponse.Content.ReadFromJsonAsync<TaskAcceptanceChecklistItemDto>(JsonOptions))!;
        toggled.IsCompleted.Should().BeTrue();
        toggled.RowVersion.Should().NotBe(firstChecklistRow.RowVersion);
        var staleToggle = await SendAsync(
            HttpMethod.Patch,
            $"/api/ai/native-actions/acceptance-checklist/{firstChecklistRow.Id}",
            new UpdateTaskAcceptanceChecklistItemRequestDto(false, firstChecklistRow.RowVersion),
            await CsrfAsync());
        staleToggle.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var breakdown = await PrepareAsync(
            AiNativeDomainActionContract.BreakdownCapability,
            "Tách Task đang mở thành 4 subtask theo thứ tự thực hiện, có dependency, estimate và required skill; mở card review trước khi tạo.",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", "task",
                seeded.ProjectId, "task", seeded.TaskId));
        breakdown.Payload.GetProperty("subtasks").GetArrayLength().Should().Be(4);
        foreach (var item in breakdown.Payload.GetProperty("subtasks").EnumerateArray())
        {
            item.GetProperty("requiredSkillId").ValueKind.Should().Be(JsonValueKind.String);
            item.GetProperty("requiredSkillName").GetString().Should().NotBeNullOrWhiteSpace();
        }
        var breakdownKey = $"breakdown-{Guid.NewGuid():N}";
        var breakdownReceipt = await ConfirmAsync(breakdown, breakdownKey);
        breakdownReceipt.Items.Should().HaveCount(4);
        var breakdownReplay = await ConfirmAsync(breakdown, breakdownKey);
        breakdownReplay.ReceiptId.Should().Be(breakdownReceipt.ReceiptId);
        breakdownReplay.Replayed.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskAcceptanceChecklistItems.CountAsync(item => item.TaskId == seeded.TaskId))
            .Should().Be(checklistReceipt.Items.Count);
        var subtasks = await db.TaskItems.AsNoTracking()
            .Where(item => item.ParentTaskId == seeded.TaskId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync();
        subtasks.Should().HaveCount(4);
        subtasks.Should().OnlyContain(item => item.ContributesToProgress);
        subtasks.Sum(item => item.EstimatedHours ?? 0).Should().Be(8,
            "breakdown must distribute the parent estimate without inventing extra capacity");
        (await db.TaskItems.Where(item => item.Id == seeded.TaskId)
            .Select(item => item.ContributesToProgress).SingleAsync()).Should().BeFalse(
                "a decomposed parent must not be counted together with its leaf subtasks");
        subtasks.Select(item => item.SortOrder).Should().Equal(Enumerable.Range(0, 4));
        (await db.TaskDependencies.CountAsync(item =>
            subtasks.Select(task => task.Id).Contains(item.PredecessorId) &&
            subtasks.Select(task => task.Id).Contains(item.SuccessorId))).Should().Be(3);
        (await db.TaskSkillRequirements.CountAsync(item =>
            subtasks.Select(task => task.Id).Contains(item.TaskItemId))).Should().Be(4);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-DOMAIN-02")]
    public async Task P18P19P22_WikiPollAndDigest_UseExactStructuredDraftsAndCanonicalReadBack()
    {
        var seeded = await SeedAsync();
        var wiki = await PrepareAsync(
            AiNativeDomainActionContract.WikiCapability,
            "Tóm tắt Wiki đang mở thành brief có link tới section nguồn; đề xuất tối đa 3 Task tùy chọn. Chỉ tạo các Task tôi tick chọn sau một xác nhận.",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}/wiki/{seeded.WikiId}", "wiki",
                seeded.ProjectId, "wiki", seeded.WikiId));
        wiki.Payload.GetProperty("sectionRefs").GetArrayLength().Should().Be(2);
        var wikiCandidates = wiki.Payload.GetProperty("taskCandidates");
        wikiCandidates.GetArrayLength().Should().Be(2);
        wikiCandidates.EnumerateArray().Should().OnlyContain(item => !item.GetProperty("selected").GetBoolean());
        var wikiPayload = JsonSerializer.Deserialize<AiNativeWikiPayloadDto>(wiki.Payload.GetRawText(), JsonOptions)!;
        var reviewedWikiPayload = JsonSerializer.SerializeToElement(wikiPayload with
        {
            TaskCandidates = wikiPayload.TaskCandidates!
                .Select((item, index) => item with { Selected = index == 0 })
                .ToArray()
        }, JsonOptions);
        var wikiUpdate = await SendAsync(
            HttpMethod.Patch,
            $"/api/ai/native-actions/{wiki.DraftId}",
            new UpdateAiNativeActionRequestDto(wiki.Revision, wiki.RowVersion, reviewedWikiPayload),
            await CsrfAsync());
        wikiUpdate.StatusCode.Should().Be(HttpStatusCode.OK, await wikiUpdate.Content.ReadAsStringAsync());
        var reviewedWiki = (await wikiUpdate.Content.ReadFromJsonAsync<AiNativeActionDraftDto>(JsonOptions))!;
        var wikiReceipt = await ConfirmAsync(reviewedWiki, $"wiki-{Guid.NewGuid():N}");
        wikiReceipt.Items.Count(item => item.EntityType == "task").Should().Be(1);
        wikiReceipt.Items.Should().ContainSingle(item => item.EntityType == "wiki_brief");

        var pollPreparedAt = DateTimeOffset.UtcNow;
        var poll = await PrepareAsync(
            AiNativeDomainActionContract.GroupPollCapability,
            "Trong Group đang mở, soạn Poll chọn phương án triển khai với 4 option rõ ràng, deadline 3 ngày và cho sửa trước khi xác nhận.",
            new AiAssistantClientContextDto($"/groups/{seeded.GroupId}/polls", "group",
                EntityType: "group", EntityId: seeded.GroupId));
        poll.Payload.GetProperty("options").GetArrayLength().Should().Be(4);
        poll.Payload.GetProperty("expiredAt").GetDateTimeOffset()
            .Should().BeCloseTo(pollPreparedAt.AddDays(3), TimeSpan.FromSeconds(10));
        var pollReceipt = await ConfirmAsync(poll, $"poll-{Guid.NewGuid():N}");
        pollReceipt.Items.Should().ContainSingle(item => item.EntityType == "group_poll");

        var digest = await PrepareAsync(
            AiNativeDomainActionContract.DigestCapability,
            "Cấu hình weekly digest cho Project này vào 09:00 thứ Hai theo timezone của tổ chức; hiện card review và đọc lại subscription sau xác nhận.",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}", "project",
                seeded.ProjectId, "project", seeded.ProjectId));
        digest.Payload.GetProperty("dayOfWeek").GetInt32().Should().Be(1);
        digest.Payload.GetProperty("localTimeMinutes").GetInt32().Should().Be(540);
        digest.Payload.GetProperty("timeZoneId").GetString().Should().Be("Asia/Ho_Chi_Minh");
        var digestReceipt = await ConfirmAsync(digest, $"digest-{Guid.NewGuid():N}");
        digestReceipt.Items.Should().ContainSingle(item => item.EntityType == "project_digest_subscription");

        var disableDigest = await PrepareAsync(
            AiNativeDomainActionContract.DigestCapability,
            "Tat weekly digest cho du an",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}", "project",
                seeded.ProjectId, "project", seeded.ProjectId));
        disableDigest.Payload.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
        await ConfirmAsync(disableDigest, $"digest-disable-{Guid.NewGuid():N}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskItems.CountAsync(item => item.ProjectId == seeded.ProjectId && item.Title.Contains("Wiki")))
            .Should().Be(0, "selected Wiki tasks use their source section title, not a hard-coded Wiki label");
        (await db.TaskItems.CountAsync(item => item.ProjectId == seeded.ProjectId && item.Description!.Contains("Nguồn:")))
            .Should().Be(1);
        var canonicalPoll = await db.GroupPolls.Include(item => item.Options)
            .SingleAsync(item => item.GroupId == seeded.GroupId);
        canonicalPoll.Question.Should().Contain("chọn phương án triển khai nào", Exactly.Once());
        canonicalPoll.Question.StartsWith("Tạo poll", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        canonicalPoll.Options.Should().HaveCount(4);
        canonicalPoll.ExpiredAt.Should().NotBeNull();
        var subscription = await db.ProjectDigestSubscriptions
            .SingleAsync(item => item.ProjectId == seeded.ProjectId && item.UserId == _factory.TestUserId);
        subscription.IsEnabled.Should().BeFalse();
        subscription.NextDeliveryAt.Should().BeNull();
        subscription.LastDeliveryStatus.Should().Be("disabled");
        subscription.DayOfWeek.Should().Be(1);
        subscription.LocalTimeMinutes.Should().Be(540);
        subscription.TimeZoneId.Should().Be("Asia/Ho_Chi_Minh");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-DOMAIN-06")]
    public async Task P20P21P23_MeetingRoadmapAndSkillEvidence_ReviewThenPersistOnlySelectedCanonicalRows()
    {
        var seeded = await SeedAsync();
        var meetingSessionId = Guid.NewGuid();
        Guid meetingImportId;
        Guid sprintId;
        DateTimeOffset originalSprintEnd;
        using (var seedScope = _factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var task = await db.TaskItems.SingleAsync(item => item.Id == seeded.TaskId);
            task.Status = "Done";
            task.AssigneeId = _factory.TestUserId;
            db.TaskAssignments.Add(new TaskAssignment
            {
                TaskItemId = task.Id,
                UserId = _factory.TestUserId,
                AssignedByUserId = _factory.TestUserId
            });
            db.TaskAcceptanceChecklistItems.Add(new TaskAcceptanceChecklistItem
            {
                TaskId = task.Id,
                Text = "Luồng E2E đã được nghiệm thu trên dữ liệu thật",
                SortOrder = 0,
                IsCompleted = true,
                CreatedByUserId = _factory.TestUserId
            });
            db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = seeded.ProjectId,
                UserId = _factory.TestUserId,
                Role = "Manager"
            });

            originalSprintEnd = DateTimeOffset.UtcNow.AddDays(7);
            var sprint = new Sprint
            {
                ProjectId = seeded.ProjectId,
                Name = "Sprint hiện tại",
                StartDate = DateTimeOffset.UtcNow.Date,
                EndDate = originalSprintEnd,
                Status = "Planning"
            };
            sprintId = sprint.Id;
            task.SprintId = sprint.Id;
            task.DueDate = originalSprintEnd.AddDays(2);
            var extraction = new MeetingExtractionPayload(
                "meeting-checknote.v1",
                new string('a', 64),
                new MeetingSummaryDto("Sprint review", DateTimeOffset.UtcNow.AddHours(-1),
                    "Đội thống nhất xử lý release và QA.", ["PM", "Dev"]),
                [
                    new MeetingActionDraftDto("Kiểm tra release", "Đối chiếu checklist release.", "High", null,
                        "PM: kiểm tra release trước thứ Sáu.", "PM"),
                    new MeetingActionDraftDto("Chuẩn bị QA handoff", "Chuẩn bị dữ liệu và kịch bản QA.", "Medium", null,
                        "Dev: chuẩn bị QA handoff.", "Dev")
                ],
                ["release", "qa"],
                [],
                [new MeetingSourceEvidenceDto("Đội thống nhất xử lý release và QA.", 0, 39)],
                [new MeetingDecisionDto("Phát hành theo từng giai đoạn", "Giảm rủi ro", "PM: phát hành theo giai đoạn", 40, 70)],
                [new MeetingRiskDto("Thiếu dữ liệu QA", "High", "QA: chưa đủ dữ liệu", 71, 92)]);
            var extractionJson = JsonSerializer.Serialize(extraction, JsonOptions);
            var aiJob = new AiJob
            {
                JobType = "AI-06_MEETING_EXTRACT",
                ProjectId = seeded.ProjectId,
                SourceType = "qaly-meet",
                SourceId = meetingSessionId.ToString(),
                ProviderHint = "local",
                Sensitive = true,
                Status = "DraftReady",
                EstimatedCostUsd = 0,
                CacheKey = Guid.NewGuid().ToString("N"),
                RequestedById = _factory.TestUserId
            };
            var aiDraft = new AiGeneratedDraft
            {
                AiJobId = aiJob.Id,
                ProjectId = seeded.ProjectId,
                DraftType = "MeetingActionItems",
                PayloadJson = extractionJson,
                OriginalPayloadJson = extractionJson,
                WorkingPayloadJson = extractionJson,
                Status = AiDraftStatuses.PendingReview
            };
            var meeting = new MeetingImport
            {
                ProjectId = seeded.ProjectId,
                ImportedById = _factory.TestUserId,
                SourceProvider = "qaly-meet",
                SourceId = meetingSessionId.ToString(),
                SourceHash = new string('b', 64),
                Title = "Sprint review",
                Summary = extraction.Meeting.Summary,
                TranscriptText = "PM: phát hành theo từng giai đoạn. QA: chưa đủ dữ liệu. Dev: chuẩn bị QA handoff.",
                ParticipantsJson = "[]",
                RawPayloadJson = "{}",
                AiJobId = aiJob.Id,
                AiDraftId = aiDraft.Id
            };
            meetingImportId = meeting.Id;
            db.AddRange(sprint, aiJob, aiDraft, meeting);
            await db.SaveChangesAsync();
        }

        var meetingDraft = await PrepareAsync(
            AiNativeDomainActionContract.MeetingActionsCapability,
            "Từ transcript cuộc họp đang mở, trích quyết định, blocker và action item. Không tạo Task tự động; cho phép map từng action item sang Task có sẵn hoặc bản nháp Task mới.",
            new AiAssistantClientContextDto(
                $"/groups/{Guid.NewGuid()}/meeting?meetingId={meetingSessionId}",
                "meeting",
                EntityType: "meeting",
                EntityId: meetingSessionId));
        meetingDraft.TargetId.Should().Be(meetingImportId);
        meetingDraft.Payload.GetProperty("decisions").GetArrayLength().Should().Be(1);
        meetingDraft.Payload.GetProperty("blockers").GetArrayLength().Should().Be(1);
        meetingDraft.Payload.GetProperty("actionItems").EnumerateArray()
            .Should().OnlyContain(item => item.GetProperty("mappingMode").GetString() == "none");
        using (var noMutationScope = _factory.Services.CreateScope())
        {
            (await noMutationScope.ServiceProvider.GetRequiredService<QalyDbContext>()
                .MeetingActionItemMappings.CountAsync(item => item.MeetingImportId == meetingImportId)).Should().Be(0);
        }
        var meetingPayload = JsonSerializer.Deserialize<AiNativeMeetingActionsPayloadDto>(
            meetingDraft.Payload.GetRawText(), JsonOptions)!;
        var reviewedMeetingPayload = JsonSerializer.SerializeToElement(meetingPayload with
        {
            ActionItems =
            [
                meetingPayload.ActionItems[0] with { MappingMode = "existing_task", ExistingTaskId = seeded.TaskId },
                meetingPayload.ActionItems[1] with { MappingMode = "new_task", ExistingTaskId = null }
            ]
        }, JsonOptions);
        var updatedMeeting = await UpdateDraftAsync(meetingDraft, reviewedMeetingPayload);
        var meetingReceipt = await ConfirmAsync(updatedMeeting, $"meeting-{Guid.NewGuid():N}");
        meetingReceipt.Items.Count(item => item.EntityType == "meeting_action_mapping").Should().Be(2);

        var roadmapDraft = await PrepareAsync(
            AiNativeDomainActionContract.RoadmapAdjustCapability,
            "Đánh giá roadmap Project hiện tại và đề xuất điều chỉnh Sprint theo dependency, capacity và deadline. Hiện before/after và không ghi trước xác nhận.",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}?tab=roadmap", "project",
                seeded.ProjectId, "project", seeded.ProjectId));
        var roadmapPayload = JsonSerializer.Deserialize<AiNativeRoadmapAdjustmentPayloadDto>(
            roadmapDraft.Payload.GetRawText(), JsonOptions)!;
        roadmapPayload.Adjustments.Should().ContainSingle(item => item.SprintId == sprintId);
        roadmapPayload.Adjustments.Should().OnlyContain(item => !item.Selected);
        using (var noRoadmapMutationScope = _factory.Services.CreateScope())
        {
            (await noRoadmapMutationScope.ServiceProvider.GetRequiredService<QalyDbContext>()
                .Set<Sprint>().Where(item => item.Id == sprintId).Select(item => item.EndDate).SingleAsync())
                .Should().Be(originalSprintEnd);
        }
        var selectedAdjustment = roadmapPayload.Adjustments.Single(item => item.SprintId == sprintId) with
        {
            Selected = true,
            AfterEnd = roadmapPayload.Adjustments.Single(item => item.SprintId == sprintId).AfterEnd.AddDays(1)
        };
        var updatedRoadmap = await UpdateDraftAsync(roadmapDraft,
            JsonSerializer.SerializeToElement(roadmapPayload with { Adjustments = [selectedAdjustment] }, JsonOptions));
        var roadmapReceipt = await ConfirmAsync(updatedRoadmap, $"roadmap-{Guid.NewGuid():N}");
        roadmapReceipt.Items.Should().ContainSingle(item => item.EntityType == "sprint");

        var evidenceDraft = await PrepareAsync(
            AiNativeDomainActionContract.SkillEvidenceCapability,
            "Với Task vừa hoàn tất, đề xuất attribution và skill evidence theo tiêu chí nghiệm thu đã xác nhận. Không dùng label hoặc tin nhắn riêng làm bằng chứng.",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", "task",
                seeded.ProjectId, "task", seeded.TaskId));
        evidenceDraft.Payload.GetProperty("acceptanceEvidence").GetArrayLength().Should().Be(1);
        evidenceDraft.Payload.GetProperty("skills").GetArrayLength().Should().Be(1);
        evidenceDraft.Payload.GetProperty("contributors").GetArrayLength().Should().Be(1);
        var evidenceReceipt = await ConfirmAsync(evidenceDraft, $"evidence-{Guid.NewGuid():N}");
        evidenceReceipt.Items.Should().ContainSingle(item => item.EntityType == "skill_evidence");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.MeetingActionItemMappings.CountAsync(item => item.MeetingImportId == meetingImportId))
            .Should().Be(2);
        (await verifyDb.TaskItems.CountAsync(item => item.ProjectId == seeded.ProjectId && item.Title == "Chuẩn bị QA handoff"))
            .Should().Be(1);
        (await verifyDb.Set<Sprint>().Where(item => item.Id == sprintId).Select(item => item.EndDate).SingleAsync())
            .Should().Be(selectedAdjustment.AfterEnd);
        (await verifyDb.TaskCompletionAttributions.CountAsync(item =>
            item.TaskItemId == seeded.TaskId && item.Status == TaskCompletionAttribution.Confirmed)).Should().Be(1);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-DOMAIN-03")]
    public async Task SourceChangedAfterReview_ConfirmReturnsConflictAndCreatesZeroMutation()
    {
        var seeded = await SeedAsync();
        var draft = await PrepareAsync(
            AiNativeDomainActionContract.ChecklistCapability,
            "Soạn checklist nghiệm thu",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", "task",
                seeded.ProjectId, "task", seeded.TaskId));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            db.TaskAcceptanceChecklistItems.Add(new TaskAcceptanceChecklistItem
            {
                TaskId = seeded.TaskId,
                Text = "Concurrent reviewer criterion",
                SortOrder = 0,
                CreatedByUserId = _factory.TestUserId
            });
            await db.SaveChangesAsync();
        }

        var response = await ConfirmResponseAsync(draft, $"stale-{Guid.NewGuid():N}");
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        var retained = await verifyDb.TaskAcceptanceChecklistItems.AsNoTracking()
            .Where(item => item.TaskId == seeded.TaskId).ToListAsync();
        retained.Should().ContainSingle();
        retained[0].Text.Should().Be("Concurrent reviewer criterion");
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-DOMAIN-05")]
    public async Task DraftEditReloadReject_PersistsRevisionAndCreatesZeroDomainRows()
    {
        var seeded = await SeedAsync();
        var draft = await PrepareAsync(
            AiNativeDomainActionContract.ChecklistCapability,
            "Soạn checklist nghiệm thu",
            new AiAssistantClientContextDto($"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", "task",
                seeded.ProjectId, "task", seeded.TaskId));
        var payload = JsonSerializer.Deserialize<AiNativeChecklistPayloadDto>(draft.Payload.GetRawText(), JsonOptions)!;
        var editedPayload = JsonSerializer.SerializeToElement(payload with
        {
            Items = ["Tiêu chí đã chỉnh và lưu trên máy chủ"]
        }, JsonOptions);
        var updateResponse = await SendAsync(
            HttpMethod.Patch,
            $"/api/ai/native-actions/{draft.DraftId}",
            new UpdateAiNativeActionRequestDto(draft.Revision, draft.RowVersion, editedPayload),
            await CsrfAsync());
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, await updateResponse.Content.ReadAsStringAsync());
        var updated = (await updateResponse.Content.ReadFromJsonAsync<AiNativeActionDraftDto>(JsonOptions))!;
        updated.Revision.Should().Be(draft.Revision + 1);
        updated.Payload.GetProperty("items")[0].GetString().Should().Be("Tiêu chí đã chỉnh và lưu trên máy chủ");

        var restored = await _client.GetFromJsonAsync<AiNativeActionDraftDto>(
            $"/api/ai/native-actions/{draft.DraftId}", JsonOptions);
        restored!.Payload.GetProperty("items")[0].GetString().Should().Be("Tiêu chí đã chỉnh và lưu trên máy chủ");
        Guid sessionId;
        using (var lookupScope = _factory.Services.CreateScope())
        {
            var lookupDb = lookupScope.ServiceProvider.GetRequiredService<QalyDbContext>();
            sessionId = await lookupDb.AssistantArtifactRefs.AsNoTracking()
                .Where(item => item.DraftId == draft.DraftId)
                .Select(item => item.Turn.SessionId)
                .SingleAsync();
        }
        var restoredSession = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{sessionId}", JsonOptions);
        restoredSession!.Turns.Single().Response!.NativeActionDraft!.Payload
            .GetProperty("items")[0].GetString().Should().Be("Tiêu chí đã chỉnh và lưu trên máy chủ");

        var rejectResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/ai/native-actions/{draft.DraftId}/reject",
            new RejectAiNativeActionRequestDto(updated.Revision, updated.RowVersion),
            await CsrfAsync());
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK, await rejectResponse.Content.ReadAsStringAsync());
        var rejected = (await rejectResponse.Content.ReadFromJsonAsync<AiNativeActionDraftDto>(JsonOptions))!;
        rejected.Status.Should().Be("rejected");
        var rejectedSession = await _client.GetFromJsonAsync<AiAssistantSessionDto>(
            $"/api/ai/assistant/sessions/{sessionId}", JsonOptions);
        rejectedSession!.Turns.Single().Response!.NativeActionDraft!.Status.Should().Be("rejected");

        var confirmRejected = await ConfirmResponseAsync(rejected, $"rejected-{Guid.NewGuid():N}");
        confirmRejected.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await db.TaskAcceptanceChecklistItems.CountAsync(item => item.TaskId == seeded.TaskId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-DOMAIN-04")]
    public async Task MemberMutationRequest_IsDeniedAtDiscoveryAndCreatesZeroCanonicalRows()
    {
        var seeded = await SeedAsync();
        var memberId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
            var organizationId = await db.Projects.Where(item => item.Id == seeded.ProjectId)
                .Select(item => item.OrganizationId!.Value)
                .SingleAsync();
            db.AddRange(
                new User
                {
                    Id = memberId,
                    FullName = "Read only member",
                    Email = $"member-{memberId:N}@qaly.test",
                    PasswordHash = "not-used",
                    Role = "User",
                    IsActive = true
                },
                new ProjectMember
                {
                    ProjectId = seeded.ProjectId,
                    UserId = memberId,
                    Role = "Member"
                },
                new OrganizationMember
                {
                    OrganizationId = organizationId,
                    UserId = memberId,
                    Role = "Member"
                });
            await db.SaveChangesAsync();
        }

        using var memberClient = _factory.CreateClient();
        memberClient.DefaultRequestHeaders.Add("X-Test-UserId", memberId.ToString());
        var csrf = (await memberClient.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;
        var context = new AiAssistantClientContextDto(
            $"/projects/{seeded.ProjectId}/tasks/{seeded.TaskId}", "task",
            seeded.ProjectId, "task", seeded.TaskId);
        using var sessionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/sessions")
        {
            Content = JsonContent.Create(new CreateAiAssistantSessionRequestDto(context, "Denied native mutation"))
        };
        sessionRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        var sessionResponse = await memberClient.SendAsync(sessionRequest);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = (await sessionResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        using var turnRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/turns")
        {
            Content = JsonContent.Create(new AiAssistantTurnRequestDto(
                "Soan acceptance checklist nghiem thu va luu ngay",
                context,
                SessionId: session.SessionId,
                ExpectedVersion: session.Version,
                ClientTurnId: Guid.NewGuid(),
                RequestedCapabilityId: AiNativeDomainActionContract.ChecklistCapability))
        };
        turnRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        turnRequest.Headers.Add("Idempotency-Key", $"denied-{Guid.NewGuid():N}");
        var turnResponse = await memberClient.SendAsync(turnRequest);
        turnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await turnResponse.Content.ReadAsStringAsync());
        var turn = (await turnResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        turn.NativeActionDraft.Should().BeNull();
        turn.Disposition.Should().NotBe("native_action_draft");
        turn.Intent.Should().Be(AiAssistantTurnContract.GuidedAnswerIntent);

        var wikiContext = new AiAssistantClientContextDto(
            $"/projects/{seeded.ProjectId}/wiki/{seeded.WikiId}", "wiki",
            seeded.ProjectId, "wiki", seeded.WikiId);
        using var wikiSessionRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/sessions")
        {
            Content = JsonContent.Create(new CreateAiAssistantSessionRequestDto(wikiContext, "Member grounded Wiki read"))
        };
        wikiSessionRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        var wikiSessionResponse = await memberClient.SendAsync(wikiSessionRequest);
        wikiSessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var wikiSession = (await wikiSessionResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        using var wikiTurnRequest = new HttpRequestMessage(HttpMethod.Post, "/api/ai/assistant/turns")
        {
            Content = JsonContent.Create(new AiAssistantTurnRequestDto(
                "Tóm tắt tài liệu này và nêu điểm chính",
                wikiContext,
                SessionId: wikiSession.SessionId,
                ExpectedVersion: wikiSession.Version,
                ClientTurnId: Guid.NewGuid(),
                RequestedCapabilityId: AiAssistantContextContract.GroundedReadCapability))
        };
        wikiTurnRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        wikiTurnRequest.Headers.Add("Idempotency-Key", $"member-wiki-{Guid.NewGuid():N}");
        var wikiTurnResponse = await memberClient.SendAsync(wikiTurnRequest);
        wikiTurnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await wikiTurnResponse.Content.ReadAsStringAsync());
        var wikiTurn = (await wikiTurnResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        wikiTurn.NativeActionDraft.Should().BeNull();
        wikiTurn.SourceRefs.Should().Contain(reference => reference.Contains($"/wiki/{seeded.WikiId}", StringComparison.Ordinal));

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<QalyDbContext>();
        (await verifyDb.TaskAcceptanceChecklistItems.CountAsync(item => item.TaskId == seeded.TaskId)).Should().Be(0);
        (await verifyDb.AiNativeActionDrafts.CountAsync(item => item.UserId == memberId)).Should().Be(0);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-QUALITY-01")]
    public async Task CompletedTurn_StreamsAnswerDeltas_AndPublishesVersionedQualityMetrics()
    {
        var seeded = await SeedAsync();
        var csrf = await CsrfAsync();
        var context = new AiAssistantClientContextDto(
            $"/projects/{seeded.ProjectId}", "project", seeded.ProjectId, "project", seeded.ProjectId);
        var sessionResponse = await SendAsync(HttpMethod.Post, "/api/ai/assistant/sessions",
            new CreateAiAssistantSessionRequestDto(context, "Quality stream"), csrf);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var session = (await sessionResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        var clientTurnId = Guid.NewGuid();
        var turnResponse = await SendAsync(HttpMethod.Post, "/api/ai/assistant/turns",
            new AiAssistantTurnRequestDto(
                "Tom tat tien do du an va neu buoc tiep theo huu ich",
                context,
                SessionId: session.SessionId,
                ExpectedVersion: session.Version,
                ClientTurnId: clientTurnId,
                RequestedCapabilityId: AiAssistantContextContract.GroundedReadCapability),
            csrf,
            $"quality-{clientTurnId:N}");
        turnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await turnResponse.Content.ReadAsStringAsync());

        using var streamResponse = await _client.GetAsync(
            $"/api/ai/assistant/sessions/{session.SessionId}/stream?clientTurnId={clientTurnId}");
        streamResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        streamResponse.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        var stream = await streamResponse.Content.ReadAsStringAsync();
        stream.Should().Contain("event: answer_delta");
        stream.Should().Contain("event: done");

        var metrics = await _client.GetFromJsonAsync<AiAssistantQualityMetricsDto>(
            "/api/ai/assistant/quality/metrics", JsonOptions);
        metrics.Should().NotBeNull();
        metrics!.EvaluationSetVersion.Should().Be(AiAssistantQualityContract.EvaluationSetVersion);
        metrics.EvaluatedTurns.Should().BeGreaterThan(0);
        metrics.FalseMutationSuccesses.Should().Be(0);
        metrics.DeadEndRate.Should().BeLessThanOrEqualTo(AiAssistantQualityContract.MaximumDeadEndRate);
        metrics.AverageLatencyMs.Should().BeLessThanOrEqualTo(AiAssistantQualityContract.MaximumInteractiveLatencyMs);
    }

    [Fact]
    [Trait("TestId", "TEST-AI-NATIVE-LEGACY-MUTATION-01")]
    public async Task LegacyPlannerAndAgentMutationEndpoints_AreRetiredInsteadOfBypassingNativeCore()
    {
        var generate = await _client.PostAsJsonAsync("/api/ai/generate-plan", new
        {
            userPrompt = "Tạo dự án bỏ qua review"
        });
        generate.StatusCode.Should().Be(HttpStatusCode.Gone);

        var csrf = await CsrfAsync();
        var create = await SendAsync(HttpMethod.Post, "/api/ai/create-plan", new
        {
            isNewProject = true,
            projectName = "Must not be created",
            tasks = Array.Empty<object>()
        }, csrf);
        create.StatusCode.Should().Be(HttpStatusCode.Gone);

        var agent = await _client.PostAsJsonAsync("/api/ai/agent-runs", new
        {
            projectId = Guid.NewGuid(),
            goal = "Mutate outside native core"
        });
        agent.StatusCode.Should().Be(HttpStatusCode.Gone);

        var legacyTaskId = Guid.NewGuid();
        var legacyProjectId = Guid.NewGuid();
        var breakdown = await SendAsync(HttpMethod.Post, $"/api/ai/tasks/{legacyTaskId}/breakdown", new
        {
            projectId = legacyProjectId,
            prompt = "Break down outside the governed draft"
        }, csrf);
        breakdown.StatusCode.Should().Be(HttpStatusCode.Gone);

        var checklist = await SendAsync(HttpMethod.Post, $"/api/ai/tasks/{legacyTaskId}/acceptance-checklist", new
        {
            projectId = legacyProjectId,
            prompt = "Create checklist outside the governed draft"
        }, csrf);
        checklist.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    private async Task<AiNativeActionDraftDto> PrepareAsync(
        string capabilityId,
        string prompt,
        AiAssistantClientContextDto context)
    {
        var csrf = await CsrfAsync();
        var sessionResponse = await SendAsync(HttpMethod.Post, "/api/ai/assistant/sessions",
            new CreateAiAssistantSessionRequestDto(context, $"Native action {capabilityId}"), csrf);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created, await sessionResponse.Content.ReadAsStringAsync());
        var session = (await sessionResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        var turn = new AiAssistantTurnRequestDto(prompt, context,
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid(),
            RequestedCapabilityId: capabilityId);
        var turnResponse = await SendAsync(HttpMethod.Post, "/api/ai/assistant/turns", turn, csrf,
            $"turn-{Guid.NewGuid():N}");
        turnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await turnResponse.Content.ReadAsStringAsync());
        var value = (await turnResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        value.Intent.Should().Be(capabilityId);
        value.Disposition.Should().Be("native_action_draft");
        value.NativeActionDraft.Should().NotBeNull();
        value.NativeActionDraft!.RendererId.Should().Be(AiNativeDomainActionContract.RendererId);
        return value.NativeActionDraft;
    }

    private async Task<AiNativeActionDraftDto> PrepareFromNaturalPromptAsync(
        string expectedCapabilityId,
        string prompt,
        AiAssistantClientContextDto context)
    {
        var csrf = await CsrfAsync();
        var sessionResponse = await SendAsync(HttpMethod.Post, "/api/ai/assistant/sessions",
            new CreateAiAssistantSessionRequestDto(context, $"Natural route {expectedCapabilityId}"), csrf);
        sessionResponse.StatusCode.Should().Be(HttpStatusCode.Created, await sessionResponse.Content.ReadAsStringAsync());
        var session = (await sessionResponse.Content.ReadFromJsonAsync<AiAssistantSessionDto>(JsonOptions))!;
        var turn = new AiAssistantTurnRequestDto(
            prompt,
            context,
            SessionId: session.SessionId,
            ExpectedVersion: session.Version,
            ClientTurnId: Guid.NewGuid());
        var turnResponse = await SendAsync(HttpMethod.Post, "/api/ai/assistant/turns", turn, csrf,
            $"natural-turn-{Guid.NewGuid():N}");
        turnResponse.StatusCode.Should().Be(HttpStatusCode.OK, await turnResponse.Content.ReadAsStringAsync());
        var value = (await turnResponse.Content.ReadFromJsonAsync<AiAssistantTurnResponseDto>(JsonOptions))!;
        value.Intent.Should().Be(expectedCapabilityId);
        value.Disposition.Should().Be("native_action_draft");
        value.NativeActionDraft.Should().NotBeNull();
        value.NativeActionDraft!.CapabilityId.Should().Be(expectedCapabilityId);
        value.NativeActionDraft.RendererId.Should().Be(AiNativeDomainActionContract.RendererId);
        return value.NativeActionDraft;
    }

    private async Task<AiNativeActionReceiptDto> ConfirmAsync(AiNativeActionDraftDto draft, string key)
    {
        var response = await ConfirmResponseAsync(draft, key);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AiNativeActionReceiptDto>(JsonOptions))!;
    }

    private async Task<AiNativeActionDraftDto> UpdateDraftAsync(AiNativeActionDraftDto draft, JsonElement payload)
    {
        var response = await SendAsync(
            HttpMethod.Patch,
            $"/api/ai/native-actions/{draft.DraftId}",
            new UpdateAiNativeActionRequestDto(draft.Revision, draft.RowVersion, payload),
            await CsrfAsync());
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AiNativeActionDraftDto>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> ConfirmResponseAsync(AiNativeActionDraftDto draft, string key)
    {
        var request = new ConfirmAiNativeActionRequestDto(draft.Revision, draft.RowVersion, draft.Payload);
        return await SendAsync(HttpMethod.Post, $"/api/ai/native-actions/{draft.DraftId}/confirm", request,
            await CsrfAsync(), key);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string url,
        object body,
        string csrf,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (idempotencyKey != null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await _client.SendAsync(request);
    }

    private async Task<string> CsrfAsync()
        => (await _client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf", JsonOptions))!.Token;

    private async Task<SeededScope> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QalyDbContext>();
        if (!await db.Users.AnyAsync(item => item.Id == _factory.TestUserId))
        {
            db.Users.Add(new User
            {
                Id = _factory.TestUserId,
                FullName = "Native AI Owner",
                Email = "native-owner@qaly.test",
                PasswordHash = "not-used",
                Role = "User",
                IsActive = true
            });
        }
        var organization = new Organization { Name = $"Native Org {Guid.NewGuid():N}", OwnerId = _factory.TestUserId };
        var project = new Project
        {
            Name = $"Native Project {Guid.NewGuid():N}",
            Code = $"NA-{Guid.NewGuid():N}"[..12],
            OwnerId = _factory.TestUserId,
            OrganizationId = organization.Id,
            Status = "Active"
        };
        var skill = new OrganizationSkill
        {
            OrganizationId = organization.Id,
            Name = "Backend / .NET",
            NormalizedName = "backend-dotnet",
            Category = "Chuyên môn",
            DefaultRequiredLevel = "Intermediate",
            IsSystemSeed = true,
            IsActive = true
        };
        var task = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = _factory.TestUserId,
            Title = "Canonical parent task",
            Description = "Luồng chính phải được xác minh. Dữ liệu phải được đọc lại.",
            Priority = "High",
            Status = "Todo",
            EstimatedHours = 8,
            RowVersion = [1, 2, 3]
        };
        var wiki = new WikiPage
        {
            ProjectId = project.Id,
            AuthorId = _factory.TestUserId,
            Title = "Wiki architecture",
            Content = "# Scope\nBuild the canonical SPA flow.\n## Acceptance\nPersist and read back the created task.",
            Visibility = "internal"
        };
        var group = new WorkGroup
        {
            Name = $"Native Group {Guid.NewGuid():N}",
            OwnerId = _factory.TestUserId,
            OrganizationId = organization.Id,
            Status = "Active"
        };
        db.AddRange(organization, project, skill, task, wiki, group);
        db.OrganizationMemberCapacityProfiles.Add(new OrganizationMemberCapacityProfile
        {
            OrganizationId = organization.Id,
            UserId = _factory.TestUserId,
            WeeklyCapacityHours = 40,
            TimeZoneId = "Asia/Ho_Chi_Minh"
        });
        db.TaskSkillRequirements.Add(new TaskSkillRequirement
        {
            TaskItemId = task.Id,
            OrganizationSkillId = skill.Id,
            RequiredLevel = "Intermediate",
            Provenance = "MANUAL",
            ConfirmedByUserId = _factory.TestUserId,
            ConfirmedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return new SeededScope(project.Id, task.Id, wiki.Id, group.Id);
    }

    private sealed record SeededScope(Guid ProjectId, Guid TaskId, Guid WikiId, Guid GroupId);
    private sealed record CsrfResponse(string Token);
}
