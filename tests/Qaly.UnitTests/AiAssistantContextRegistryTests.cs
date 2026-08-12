using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiAssistantContextRegistryTests
{
    [Fact]
    public async Task ResolveAsync_ForOwnerTaskCreate_ReturnsTypedCapabilityAndDeterministicSources()
    {
        await using var db = CreateContext();
        var owner = CreateUser("Owner");
        var project = CreateProject(owner.Id);
        db.AddRange(owner, project);
        await db.SaveChangesAsync();

        var result = await CreateRegistry(db, owner.Id).ResolveAsync(new AiAssistantTurnRequestDto(
            "Tạo task frontend và backend",
            new AiAssistantClientContextDto(ProjectId: project.Id)));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Capabilities.Should().Contain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability &&
            item.ConfirmationPolicy == "explicit_selective_confirm" &&
            item.RendererId == "task-plan-review.v1");
        result.Data.Sources.Select(item => item.SourceId).Should().BeEquivalentTo(
            AiAssistantContextContract.ProjectSummarySource,
            AiAssistantContextContract.ProjectMembersSource,
            AiAssistantContextContract.ProjectSkillsSource,
            AiAssistantContextContract.ProjectWorkloadSource);
        result.Data.Sources.Should().OnlyContain(item =>
            item.ContentHash.StartsWith("sha256:", StringComparison.Ordinal) &&
            item.RetrievalMethod == "deterministic" &&
            item.TrustClass == "qaly_domain_record");
        result.Data.SourceDisclosures.Should().OnlyContain(item => item.Status == "read");
    }

    [Fact]
    public async Task ResolveAsync_WithUnknownCapabilityOrSource_FailsClosedBeforeRetrieval()
    {
        await using var db = CreateContext();
        var user = CreateUser("Requester");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var registry = CreateRegistry(db, user.Id);

        var unknownCapability = await registry.ResolveAsync(new AiAssistantTurnRequestDto(
            "Phân tích dự án",
            RequestedCapabilityId: "shell.execute.v1"));
        var unknownSource = await registry.ResolveAsync(new AiAssistantTurnRequestDto(
            "Phân tích dự án",
            RequestedSourceIds: ["repository.secret"]));

        unknownCapability.IsSuccess.Should().BeFalse();
        unknownCapability.StatusCode.Should().Be(400);
        unknownCapability.ErrorCode.Should().Be("assistant_capability_unknown");
        unknownSource.IsSuccess.Should().BeFalse();
        unknownSource.StatusCode.Should().Be(400);
        unknownSource.ErrorCode.Should().Be("assistant_source_unknown");
    }

    [Fact]
    public async Task ResolveAsync_ProjectLaunch_UsesOrganizationAndEffectiveRulebookSourcesOnly()
    {
        await using var db = CreateContext();
        var owner = CreateUser("Organization owner");
        var organization = new Organization
        {
            Name = "Launch workspace",
            Code = "LAUNCH",
            OwnerId = owner.Id,
            IsActive = true
        };
        db.AddRange(owner, organization,
            new OrganizationMember { OrganizationId = organization.Id, UserId = owner.Id, Role = OrganizationRoleRules.Owner },
            new OrganizationWorkRuleSet
            {
                OrganizationId = organization.Id,
                Version = 1,
                Status = "active",
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
                RulesJson = "[]",
                CreatedByUserId = owner.Id,
                Revision = 1
            });
        await db.SaveChangesAsync();

        var result = await CreateRegistry(db, owner.Id).ResolveAsync(new AiAssistantTurnRequestDto(
            "Khởi chạy một dự án web SPA",
            new AiAssistantClientContextDto(OrganizationId: organization.Id),
            RequestedCapabilityId: AiProjectLaunchContract.CapabilityId));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Capabilities.Should().Contain(item => item.CapabilityId == AiProjectLaunchContract.CapabilityId);
        result.Data.Capabilities.Should().Contain(item =>
            item.CapabilityId == AiProjectOrchestrationContract.StaffingCapabilityId &&
            item.RiskClass == "read_only_proposal" &&
            item.OutputSchemaId == AiProjectOrchestrationContract.PlanSchemaId);
        result.Data.Capabilities.Should().Contain(item =>
            item.CapabilityId == AiProjectOrchestrationContract.ExecuteCapabilityId &&
            item.ConfirmationPolicy == "explicit_batch_confirm" &&
            item.RollbackPolicy == "impact_checked_soft_delete");
        result.Data.Sources.Select(item => item.SourceId).Should().Contain(
            AiAssistantContextContract.OrganizationSummarySource,
            AiAssistantContextContract.OrganizationRulebookSource);
        result.Data.Sources.Should().OnlyContain(item => item.PrivacyClass == "organization_private");
    }

    [Fact]
    public async Task DiscoverAsync_ForManagedOrganizationWithoutProjects_AdvertisesPlanningExecutionAndMonitoring()
    {
        await using var db = CreateContext();
        var owner = CreateUser("Organization owner");
        var organization = new Organization
        {
            Name = "Empty launch workspace",
            Code = "EMPTY-LAUNCH",
            OwnerId = owner.Id,
            IsActive = true
        };
        db.AddRange(owner, organization,
            new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = owner.Id,
                Role = OrganizationRoleRules.Owner
            });
        await db.SaveChangesAsync();

        var result = await CreateRegistry(db, owner.Id).DiscoverAsync(new AiAssistantTurnRequestDto(
            "Plan and launch the first Project",
            new AiAssistantClientContextDto(OrganizationId: organization.Id)));

        result.IsSuccess.Should().BeTrue(result.Error);
        var capabilityIds = result.Data!.Capabilities.Select(item => item.CapabilityId);
        capabilityIds.Should().Contain(AiProjectOrchestrationContract.StaffingCapabilityId);
        capabilityIds.Should().Contain(AiProjectOrchestrationContract.ExecuteCapabilityId);
        capabilityIds.Should().Contain(AiProjectOrchestrationContract.MonitorCapabilityId);
        result.Data.Capabilities.Should().NotContain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability);
    }

    [Fact]
    public async Task ResolveAsync_ForForeignProject_ReturnsNondisclosingNotFound()
    {
        await using var db = CreateContext();
        var owner = CreateUser("Foreign owner");
        var requester = CreateUser("Requester");
        var project = CreateProject(owner.Id);
        db.AddRange(owner, requester, project);
        await db.SaveChangesAsync();

        var result = await CreateRegistry(db, requester.Id).ResolveAsync(new AiAssistantTurnRequestDto(
            "Phân tích dự án",
            new AiAssistantClientContextDto(ProjectId: project.Id)));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ResolveAsync_ForMember_ExcludesPrivateTaskBeforeContextMaterialization()
    {
        await using var db = CreateContext();
        var owner = CreateUser("Owner");
        var member = CreateUser("Member");
        var project = CreateProject(owner.Id);
        var membership = new ProjectMember
        {
            ProjectId = project.Id,
            UserId = member.Id,
            Role = "Member"
        };
        var publicTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "Visible delivery task",
            Status = "Todo"
        };
        var privateTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "PRIVATE-SALARY-DISCUSSION",
            Status = "Todo",
            IsPrivate = true
        };
        db.AddRange(owner, member, project, membership, publicTask, privateTask);
        await db.SaveChangesAsync();

        var result = await CreateRegistry(db, member.Id).ResolveAsync(new AiAssistantTurnRequestDto(
            "Phân tích các task đang có",
            new AiAssistantClientContextDto(ProjectId: project.Id)));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Capabilities.Should().Contain(item =>
            item.CapabilityId == AiAssistantContextContract.GroundedReadCapability);
        result.Data.Capabilities.Should().Contain(item =>
            item.CapabilityId == AiAssistantContextContract.ResearchPlanCapability);
        result.Data.Capabilities.Should().NotContain(item =>
            item.CapabilityId == AiAssistantContextContract.TaskCreateCapability);
        result.Data.Capabilities.Should().NotContain(item =>
            item.CapabilityId == AiProjectOrchestrationContract.MonitorCapabilityId);
        var tasksSource = result.Data.Sources.Single(item =>
            item.SourceId == AiAssistantContextContract.ProjectTasksSource);
        var serializedFacts = JsonSerializer.Serialize(tasksSource.Facts);
        serializedFacts.Should().Contain("Visible delivery task");
        serializedFacts.Should().NotContain("PRIVATE-SALARY-DISCUSSION");
        tasksSource.Redactions.Should().Contain("restricted_records_excluded");
    }

    [Fact]
    public async Task ResolveAsync_ForRestrictedSelectedTask_DisclosesDenyWithoutTitleOrEnvelope()
    {
        await using var db = CreateContext();
        var owner = CreateUser("Owner");
        var member = CreateUser("Member");
        var project = CreateProject(owner.Id);
        var membership = new ProjectMember { ProjectId = project.Id, UserId = member.Id, Role = "Member" };
        var privateTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "PRIVATE-BOARD-MATTER",
            IsPrivate = true
        };
        db.AddRange(owner, member, project, membership, privateTask);
        await db.SaveChangesAsync();

        var result = await CreateRegistry(db, member.Id).ResolveAsync(new AiAssistantTurnRequestDto(
            "Giải thích task này",
            new AiAssistantClientContextDto(
                ProjectId: project.Id,
                EntityType: "task",
                EntityId: privateTask.Id)));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Sources.Should().NotContain(item => item.SourceId == AiAssistantContextContract.TaskDetailSource);
        result.Data.SourceDisclosures.Should().Contain(item =>
            item.SourceId == AiAssistantContextContract.TaskDetailSource &&
            item.Status == "denied" &&
            item.SourceRef == null);
        JsonSerializer.Serialize(result.Data).Should().NotContain("PRIVATE-BOARD-MATTER");
    }

    private static AiAssistantContextRegistry CreateRegistry(QalyDbContext db, Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        currentUser.SetupGet(item => item.Role).Returns("User");
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        return new AiAssistantContextRegistry(
            db,
            currentUser.Object,
            Options.Create(new AiJobPlatformOptions
            {
                AssistantContextRegistryEnabled = true,
                AssistantResearchPlanEnabled = true,
                ProjectLaunchBriefEnabled = true,
                ProjectLaunchPlanningEnabled = true,
                ProjectLaunchExecutionEnabled = true,
                ProjectOperationMonitoringEnabled = true,
                ActionComposerEnabled = true,
                ActionComposerTaskCreateEnabled = true
            }));
    }

    private static QalyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User CreateUser(string name)
        => new()
        {
            FullName = name,
            Email = $"{Guid.NewGuid():N}@assistant-context.test",
            PasswordHash = "not-used",
            Role = "User",
            IsActive = true
        };

    private static Project CreateProject(Guid ownerId)
        => new()
        {
            Name = "Assistant Context Project",
            Code = "ACP",
            OwnerId = ownerId,
            Status = "Active"
        };
}
