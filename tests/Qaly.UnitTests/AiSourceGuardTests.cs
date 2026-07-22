using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Qaly.Application.Common.Models;
using Qaly.Application.DTOs.Ai;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services.AI;

namespace Qaly.UnitTests;

public sealed class AiSourceGuardTests
{
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
        var guard = new AiSourceGuard(db);

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

        var result = await new AiSourceGuard(db).ValidateAsync(
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

        var result = await new AiSourceGuard(db).CaptureAsync(
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

        var result = await new AiSourceGuard(db).CaptureAsync(
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

        var result = await new AiSourceGuard(db).CaptureAsync(
            project.Id,
            owner.Id,
            [new AiJobSourceInputDto("message", message.Id, null, null, null)],
            CancellationToken.None);

        result.IsAllowed.Should().BeFalse();
        result.ErrorCode.Should().Be(AiErrorCodes.PermissionDenied);
    }

    private static QalyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
