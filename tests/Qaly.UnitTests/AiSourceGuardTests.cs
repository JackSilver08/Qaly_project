using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiSourceGuardTests
{
    [Fact]
    public async Task ValidateAsync_WhenProgressTaskChanges_ProjectSnapshotBecomesStale()
    {
        await using var db = CreateContext();
        var owner = new User
        {
            FullName = "Progress Owner",
            Email = $"progress-owner-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "not-used",
            Role = "User"
        };
        var project = new Project
        {
            Name = "Progress Project",
            Code = "PROG",
            OwnerId = owner.Id
        };
        var task = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "Grounded task",
            Status = "InProgress",
            ContributesToProgress = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        db.AddRange(owner, project, task);
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);
        var captured = await guard.CaptureAsync(
            project.Id,
            owner.Id,
            [new AiJobSourceInputDto("project", project.Id, null, null, null)],
            CancellationToken.None);

        task.Status = "Done";
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var validation = await guard.ValidateAsync(
            project.Id,
            owner.Id,
            captured.Sources,
            enforceFreshness: true);

        validation.IsAllowed.Should().BeFalse();
        validation.ErrorCode.Should().Be(AiErrorCodes.SourceStale);
    }

    [Fact]
    public async Task ValidateAsync_WhenSprintTaskChanges_SprintSnapshotBecomesStale()
    {
        await using var db = CreateContext();
        var owner = new User
        {
            FullName = "Sprint Owner",
            Email = $"sprint-owner-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "not-used",
            Role = "User"
        };
        var project = new Project { Name = "Sprint Project", Code = "SPR", OwnerId = owner.Id };
        var sprint = new Sprint
        {
            ProjectId = project.Id,
            Name = "Release milestone",
            StartDate = DateTimeOffset.UtcNow.AddDays(-3),
            EndDate = DateTimeOffset.UtcNow.AddDays(4),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
        };
        var sprintTask = new TaskItem
        {
            ProjectId = project.Id,
            SprintId = sprint.Id,
            ReporterId = owner.Id,
            Title = "Sprint task",
            Status = "InProgress",
            ContributesToProgress = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        var outsideTask = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "Outside sprint",
            Status = "InProgress",
            ContributesToProgress = true,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        db.AddRange(owner, project, sprint, sprintTask, outsideTask);
        await db.SaveChangesAsync();
        var guard = CreateGuard(db);
        var captured = await guard.CaptureAsync(
            project.Id,
            owner.Id,
            [new AiJobSourceInputDto("sprint", sprint.Id, null, null, null)],
            CancellationToken.None);

        outsideTask.Status = "Done";
        outsideTask.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        (await guard.ValidateAsync(project.Id, owner.Id, captured.Sources, enforceFreshness: true))
            .IsAllowed.Should().BeTrue();

        sprintTask.Status = "Done";
        sprintTask.UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1);
        await db.SaveChangesAsync();
        var stale = await guard.ValidateAsync(
            project.Id,
            owner.Id,
            captured.Sources,
            enforceFreshness: true);

        stale.IsAllowed.Should().BeFalse();
        stale.ErrorCode.Should().Be(AiErrorCodes.SourceStale);
    }

    [Fact]
    public async Task ValidateAsync_WhenTaskHashChanged_ReturnsSourceStale()
    {
        await using var db = CreateContext();
        var owner = new User
        {
            FullName = "Source Owner",
            Email = $"source-owner-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "not-used",
            Role = "User"
        };
        var project = new Project
        {
            Name = "Source Guard Project",
            Code = "SRC-GUARD",
            OwnerId = owner.Id
        };
        var task = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "Original source",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };
        db.Users.Add(owner);
        db.Projects.Add(project);
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var source = new AiJobSourceInputDto(
            "task",
            task.Id,
            LegacySourceKey: null,
            SourceVersion: null,
            SourceHash: new string('f', 64),
            SourceTimestamp: null);
        var guard = CreateGuard(db);

        var stale = await guard.ValidateAsync(project.Id, owner.Id, [source], enforceFreshness: true);
        stale.IsAllowed.Should().BeFalse();
        stale.ErrorCode.Should().Be(AiErrorCodes.SourceStale);

        var freshnessNotRequired = await guard.ValidateAsync(project.Id, owner.Id, [source], enforceFreshness: false);
        freshnessNotRequired.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenPrivateTaskIsInvisible_ReturnsPermissionDenied()
    {
        await using var db = CreateContext();
        var owner = new User
        {
            FullName = "Private Source Owner",
            Email = $"private-owner-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "not-used",
            Role = "User"
        };
        var member = new User
        {
            FullName = "Project Member",
            Email = $"private-member-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "not-used",
            Role = "User"
        };
        var project = new Project
        {
            Name = "Private Source Project",
            Code = "SRC-PRIVATE",
            OwnerId = owner.Id
        };
        var task = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "Owner-only source",
            IsPrivate = true
        };
        db.Users.AddRange(owner, member);
        db.Projects.Add(project);
        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = member.Id,
            Role = ProjectRoleRules.Member
        });
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db).ValidateAsync(
            project.Id,
            member.Id,
            [new AiJobSourceInputDto("task", task.Id, null, null, null, null)],
            enforceFreshness: true);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be(AiErrorCodes.PermissionDenied);
    }

    [Fact]
    public async Task CaptureAsync_WhenMessageIsAuthorized_CapturesCanonicalVersionAndHash()
    {
        await using var db = CreateContext();
        var owner = new User { FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.test", PasswordHash = "test" };
        var group = new WorkGroup { Name = "Delivery", OwnerId = owner.Id };
        var project = new Project { Name = "Delivery Project", Code = "DEL", OwnerId = owner.Id, SourceGroupId = group.Id };
        var message = new GroupMessage
        {
            WorkGroupId = group.Id,
            UserId = owner.Id,
            Content = "Prepare the release checklist",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AddRange(owner, group, project, message);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db).CaptureAsync(
            project.Id,
            owner.Id,
            [new AiJobSourceInputDto("message", message.Id, null, null, null)],
            CancellationToken.None);

        result.IsAllowed.Should().BeTrue(result.ErrorMessage);
        result.Sources.Should().ContainSingle();
        result.Sources[0].SourceHash.Should().HaveLength(64);
        result.Sources[0].SourceVersion.Should().NotBeNullOrWhiteSpace();
        result.Sources[0].SourceTimestamp.Should().Be(message.UpdatedAt);
    }

    [Fact]
    public async Task CaptureAsync_WhenAdminCanSeeLinkedGroupMessage_AllowsCanonicalCapture()
    {
        await using var db = CreateContext();
        var admin = new User
        {
            FullName = "Admin",
            Email = $"admin-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "test",
            Role = "Admin"
        };
        var owner = new User
        {
            FullName = "Owner",
            Email = $"owner-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "test",
            Role = "User"
        };
        var group = new WorkGroup { Name = "Linked group", OwnerId = owner.Id };
        var project = new Project
        {
            Name = "Linked project",
            Code = "ADMIN-SOURCE",
            OwnerId = owner.Id,
            SourceGroupId = group.Id
        };
        var message = new GroupMessage
        {
            WorkGroupId = group.Id,
            UserId = owner.Id,
            Content = "Visible to an administrator",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.AddRange(admin, owner, group, project, message);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db).CaptureAsync(
            project.Id,
            admin.Id,
            [new AiJobSourceInputDto("message", message.Id, null, null, null)],
            CancellationToken.None);

        result.IsAllowed.Should().BeTrue(result.ErrorMessage);
        result.Sources.Should().ContainSingle();
        result.Sources[0].SourceHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CaptureAsync_WhenMessageWasDeleted_ReturnsPermissionDenied()
    {
        await using var db = CreateContext();
        var owner = new User { FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.test", PasswordHash = "test" };
        var group = new WorkGroup { Name = "Delivery", OwnerId = owner.Id };
        var project = new Project { Name = "Delivery Project", Code = "DEL", OwnerId = owner.Id, SourceGroupId = group.Id };
        var message = new GroupMessage
        {
            WorkGroupId = group.Id,
            UserId = owner.Id,
            Content = "Deleted source",
            IsDeleted = true,
            DeletedAt = DateTimeOffset.UtcNow
        };
        db.AddRange(owner, group, project, message);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db).CaptureAsync(
            project.Id,
            owner.Id,
            [new AiJobSourceInputDto("message", message.Id, null, null, null)],
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be(AiErrorCodes.PermissionDenied);
    }

    [Fact]
    public async Task CaptureAsync_OrdinaryOrganizationMemberCannotReadProjectWithoutProjectMembership()
    {
        await using var db = CreateContext();
        var owner = new User { FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.test", PasswordHash = "test", IsActive = true };
        var member = new User { FullName = "Member", Email = $"member-{Guid.NewGuid():N}@qaly.test", PasswordHash = "test", IsActive = true };
        var organization = new Organization
        {
            Name = "Tenant",
            Code = $"tenant-{Guid.NewGuid():N}",
            OwnerId = owner.Id,
            IsActive = true
        };
        var project = new Project
        {
            Name = "Restricted project",
            Code = $"restricted-{Guid.NewGuid():N}",
            OwnerId = owner.Id,
            OrganizationId = organization.Id
        };
        db.AddRange(
            owner,
            member,
            organization,
            new OrganizationMember { OrganizationId = organization.Id, UserId = owner.Id, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organization.Id, UserId = member.Id, Role = OrganizationRoleRules.Member },
            project);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db).CaptureAsync(
            project.Id,
            member.Id,
            [new AiJobSourceInputDto("project", project.Id, null, null, null)],
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be(AiErrorCodes.PermissionDenied);
    }

    [Fact]
    public async Task CaptureAsync_OrganizationAdminCanReadPortfolioProject()
    {
        await using var db = CreateContext();
        var owner = new User { FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.test", PasswordHash = "test", IsActive = true };
        var admin = new User { FullName = "Organization admin", Email = $"admin-{Guid.NewGuid():N}@qaly.test", PasswordHash = "test", IsActive = true };
        var organization = new Organization
        {
            Name = "Tenant",
            Code = $"tenant-{Guid.NewGuid():N}",
            OwnerId = owner.Id,
            IsActive = true
        };
        var project = new Project
        {
            Name = "Portfolio project",
            Code = $"portfolio-{Guid.NewGuid():N}",
            OwnerId = owner.Id,
            OrganizationId = organization.Id
        };
        db.AddRange(
            owner,
            admin,
            organization,
            new OrganizationMember { OrganizationId = organization.Id, UserId = owner.Id, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organization.Id, UserId = admin.Id, Role = OrganizationRoleRules.OrganizationAdmin },
            project);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db).CaptureAsync(
            project.Id,
            admin.Id,
            [new AiJobSourceInputDto("project", project.Id, null, null, null)],
            CancellationToken.None);

        result.IsAllowed.Should().BeTrue(result.ErrorMessage);
        result.Sources.Should().ContainSingle();
    }

    private static QalyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AiSourceGuard CreateGuard(QalyDbContext db)
    {
        var roleCatalog = new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(db));
        var authorization = new AiNativeAuthorizationService(
            new GenericRepository<SystemModulePermission>(db),
            new GenericRepository<ProjectMember>(db),
            new GenericRepository<OrganizationMember>(db),
            new GenericRepository<Organization>(db),
            roleCatalog);
        return new AiSourceGuard(db, authorization);
    }
}
