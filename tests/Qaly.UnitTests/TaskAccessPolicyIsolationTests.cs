using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public class TaskAccessPolicyIsolationTests : IDisposable
{
    private readonly QalyDbContext _context;

    public TaskAccessPolicyIsolationTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task ApplyVisibilityFilter_HidesCrossProjectAndUnassignedPrivateTasks()
    {
        var currentUserId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherOwnerId = Guid.NewGuid();
        var visibleProjectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var visibleTaskId = Guid.NewGuid();
        var crossProjectTaskId = Guid.NewGuid();
        var privateTaskId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = currentUserId, FullName = "Member", Email = "member@qaly.dev", IsActive = true },
            new User { Id = ownerId, FullName = "Owner", Email = "owner@qaly.dev", IsActive = true },
            new User { Id = otherOwnerId, FullName = "Other Owner", Email = "other-owner@qaly.dev", IsActive = true });

        _context.Projects.AddRange(
            new Project { Id = visibleProjectId, Name = "Allowed", Code = "allowed", OwnerId = ownerId },
            new Project { Id = otherProjectId, Name = "Other Tenant", Code = "other-tenant", OwnerId = otherOwnerId });

        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = visibleProjectId,
            UserId = currentUserId,
            Role = ProjectRoleRules.Member
        });

        _context.TaskItems.AddRange(
            new TaskItem
            {
                Id = visibleTaskId,
                ProjectId = visibleProjectId,
                ReporterId = ownerId,
                Title = "Visible public task"
            },
            new TaskItem
            {
                Id = crossProjectTaskId,
                ProjectId = otherProjectId,
                ReporterId = otherOwnerId,
                Title = "Cross project task"
            },
            new TaskItem
            {
                Id = privateTaskId,
                ProjectId = visibleProjectId,
                ReporterId = ownerId,
                Title = "Private task",
                IsPrivate = true
            });

        await _context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(currentUserId);
        currentUser.SetupGet(user => user.Role).Returns("Member");
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context));

        var visibleIds = await policy.ApplyVisibilityFilter(_context.TaskItems)
            .Select(task => task.Id)
            .ToListAsync();

        visibleIds.Should().ContainSingle().Which.Should().Be(visibleTaskId);
        visibleIds.Should().NotContain(crossProjectTaskId);
        visibleIds.Should().NotContain(privateTaskId);
    }

    [Fact]
    public async Task CanManageTaskAsync_DeniesFormerMemberEvenWhenHistoricalAssignmentRemains()
    {
        var formerMemberId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Project = new Project { Id = projectId, Name = "Revoked project", OwnerId = ownerId },
            ReporterId = ownerId,
            AssigneeId = formerMemberId,
            Title = "Historical assignment"
        };

        _context.Users.AddRange(
            new User { Id = formerMemberId, FullName = "Former member", Email = "former@qaly.dev", IsActive = true },
            new User { Id = ownerId, FullName = "Owner", Email = "owner-revoke@qaly.dev", IsActive = true });
        _context.Projects.Add(task.Project);
        _context.TaskItems.Add(task);
        await _context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(formerMemberId);
        currentUser.SetupGet(user => user.Role).Returns(ProjectRoleRules.Member);
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context));

        var canManage = await policy.CanManageTaskAsync(task, CancellationToken.None);

        canManage.Should().BeFalse("revoking project access must override stale task assignments");
    }

    [Fact]
    public async Task CanAccessProjectAsync_RequiresBothTenantAndProjectMembership()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = "owner-boundary@qaly.dev", IsActive = true },
            new User { Id = memberId, FullName = "Member", Email = "member-boundary@qaly.dev", IsActive = true });
        _context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Tenant",
            Code = "TEN",
            OwnerId = ownerId,
            IsActive = true
        });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Scoped",
            Code = "SCP",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = memberId,
            Role = OrganizationRoleRules.Member
        });
        await _context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(memberId);
        currentUser.SetupGet(user => user.Role).Returns(ProjectRoleRules.Member);
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context));

        (await policy.CanAccessProjectAsync(projectId, ownerId, CancellationToken.None))
            .Should().BeFalse("organization membership does not grant a project role");

        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = memberId,
            Role = ProjectRoleRules.Member
        });
        await _context.SaveChangesAsync();
        (await policy.CanAccessProjectAsync(projectId, ownerId, CancellationToken.None)).Should().BeTrue();

        _context.OrganizationMembers.Remove(await _context.OrganizationMembers.SingleAsync());
        await _context.SaveChangesAsync();
        (await policy.CanAccessProjectAsync(projectId, ownerId, CancellationToken.None))
            .Should().BeFalse("revoking tenant membership invalidates stale project membership");
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
