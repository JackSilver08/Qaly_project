using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class ToolParameterGuardAuthorizationTests
{
    [Fact]
    public async Task OrdinaryOrganizationMemberCannotUseProjectToolWithoutProjectMembership()
    {
        await using var db = CreateContext();
        var owner = User("Owner");
        var member = User("Member");
        var organization = Organization(owner.Id);
        var project = Project(owner.Id, organization.Id);
        db.AddRange(
            owner,
            member,
            organization,
            new OrganizationMember { OrganizationId = organization.Id, UserId = owner.Id, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organization.Id, UserId = member.Id, Role = OrganizationRoleRules.Member },
            project);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db, member.Id).GuardAsync(
            "GetProjectSummary",
            new Dictionary<string, object?> { ["projectId"] = project.Id });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task OrganizationAdminCanUsePortfolioProjectToolWithoutProjectMembership()
    {
        await using var db = CreateContext();
        var owner = User("Owner");
        var admin = User("Organization admin");
        var organization = Organization(owner.Id);
        var project = Project(owner.Id, organization.Id);
        db.AddRange(
            owner,
            admin,
            organization,
            new OrganizationMember { OrganizationId = organization.Id, UserId = owner.Id, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = organization.Id, UserId = admin.Id, Role = OrganizationRoleRules.OrganizationAdmin },
            project);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db, admin.Id).GuardAsync(
            "GetProjectSummary",
            new Dictionary<string, object?> { ["projectId"] = project.Id });

        result.Success.Should().BeTrue(result.UserMessage);
    }

    [Fact]
    public async Task PrivateTaskIdIsNotDisclosedToUnrelatedProjectMember()
    {
        await using var db = CreateContext();
        var owner = User("Owner");
        var member = User("Member");
        var project = Project(owner.Id, null);
        var task = new TaskItem
        {
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "Private",
            IsPrivate = true
        };
        db.AddRange(
            owner,
            member,
            project,
            new ProjectMember { ProjectId = project.Id, UserId = member.Id, Role = ProjectRoleRules.Member },
            task);
        await db.SaveChangesAsync();

        var result = await CreateGuard(db, member.Id).GuardAsync(
            "AddComment",
            new Dictionary<string, object?>
            {
                ["taskId"] = task.Id,
                ["content"] = "should not be accepted"
            });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("TASK_NOT_FOUND");
        result.UserMessage.Should().NotContain(task.Title);
    }

    private static ToolParameterGuard CreateGuard(QalyDbContext db, Guid userId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(item => item.UserId).Returns(userId);
        currentUser.SetupGet(item => item.Role).Returns(SystemRoleRules.Member);
        currentUser.SetupGet(item => item.IsAuthenticated).Returns(true);
        var projects = new GenericRepository<Project>(db);
        var members = new GenericRepository<ProjectMember>(db);
        var taskPolicy = new TaskAccessPolicy(
            currentUser.Object,
            projects,
            members,
            new GenericRepository<OrganizationMember>(db),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(db)));

        return new ToolParameterGuard(
            projects,
            new GenericRepository<TaskItem>(db),
            members,
            new GenericRepository<User>(db),
            currentUser.Object,
            taskPolicy);
    }

    private static User User(string name)
        => new()
        {
            FullName = name,
            Email = $"{name.Replace(' ', '-').ToLowerInvariant()}-{Guid.NewGuid():N}@qaly.test",
            PasswordHash = "test",
            Role = SystemRoleRules.Member,
            IsActive = true
        };

    private static Organization Organization(Guid ownerId)
        => new()
        {
            Name = "Tenant",
            Code = $"tenant-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            IsActive = true
        };

    private static Project Project(Guid ownerId, Guid? organizationId)
        => new()
        {
            Name = "Project",
            Code = $"project-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            OrganizationId = organizationId
        };

    private static QalyDbContext CreateContext()
        => new(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
