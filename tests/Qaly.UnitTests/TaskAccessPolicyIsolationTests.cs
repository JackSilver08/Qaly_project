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
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

        var visibleIds = await policy.ApplyVisibilityFilter(_context.TaskItems)
            .Select(task => task.Id)
            .ToListAsync();

        visibleIds.Should().ContainSingle().Which.Should().Be(visibleTaskId);
        visibleIds.Should().NotContain(crossProjectTaskId);
        visibleIds.Should().NotContain(privateTaskId);
    }

    [Fact]
    public async Task PrivateTask_IsVisibleToBuiltInAndCustomProjectManagers_ButNotOrdinaryMember()
    {
        var ownerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var customManagerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        const string customRoleKey = "delivery-lead";

        _context.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
            new User { Id = managerId, FullName = "Manager", Email = $"manager-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
            new User { Id = customManagerId, FullName = "Delivery lead", Email = $"lead-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
            new User { Id = memberId, FullName = "Member", Email = $"member-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
        _context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Private task tenant",
            Code = $"private-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            IsActive = true
        });
        _context.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = organizationId, UserId = managerId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organizationId, UserId = customManagerId, Role = OrganizationRoleRules.Member },
            new OrganizationMember { OrganizationId = organizationId, UserId = memberId, Role = OrganizationRoleRules.Member });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Private task project",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        _context.ProjectMembers.AddRange(
            new ProjectMember { ProjectId = projectId, UserId = managerId, Role = ProjectRoleRules.Manager },
            new ProjectMember { ProjectId = projectId, UserId = customManagerId, Role = customRoleKey },
            new ProjectMember { ProjectId = projectId, UserId = memberId, Role = ProjectRoleRules.Member });
        _context.ProjectRoleDefinitions.Add(new ProjectRoleDefinition
        {
            OrganizationId = organizationId,
            Key = customRoleKey,
            DisplayName = "Delivery lead",
            BaseRole = ProjectRoleRules.Manager,
            CreatedByUserId = ownerId,
            IsActive = true
        });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Private executive decision",
            IsPrivate = true
        });
        await _context.SaveChangesAsync();

        TaskAccessPolicy PolicyFor(Guid userId)
        {
            var currentUser = new Mock<ICurrentUserService>();
            currentUser.SetupGet(user => user.UserId).Returns(userId);
            currentUser.SetupGet(user => user.Role).Returns(SystemRoleRules.Member);
            return new TaskAccessPolicy(
                currentUser.Object,
                new GenericRepository<Project>(_context),
                new GenericRepository<ProjectMember>(_context),
                new GenericRepository<OrganizationMember>(_context),
                new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));
        }

        async Task<IReadOnlyList<Guid>> VisibleIds(Guid userId)
            => await PolicyFor(userId).ApplyVisibilityFilter(_context.TaskItems)
                .Select(task => task.Id)
                .ToListAsync();

        (await VisibleIds(managerId)).Should().Contain(taskId);
        (await VisibleIds(customManagerId)).Should().Contain(taskId);
        (await VisibleIds(memberId)).Should().NotContain(taskId);

        var privateTask = await _context.TaskItems
            .Include(task => task.Project)
            .ThenInclude(project => project.Organization)
            .Include(task => task.Assignees)
            .SingleAsync(task => task.Id == taskId);
        (await PolicyFor(customManagerId).CanAccessTaskAsync(privateTask, CancellationToken.None)).Should().BeTrue();
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
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

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
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

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

    [Fact]
    public async Task OrganizationAdminHasPortfolioAuthorityButPrivateTaskStillDeniesNonParticipant()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var privateTask = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Owner private task",
            IsPrivate = true
        };
        _context.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
            new User { Id = adminId, FullName = "Organization admin", Email = $"admin-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
        _context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Portfolio tenant",
            Code = $"portfolio-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            IsActive = true
        });
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = adminId,
            Role = OrganizationRoleRules.OrganizationAdmin
        });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Portfolio project",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        _context.TaskItems.Add(privateTask);
        await _context.SaveChangesAsync();
        privateTask.Project = await _context.Projects
            .Include(project => project.Organization)
            .SingleAsync(project => project.Id == projectId);

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(adminId);
        currentUser.SetupGet(user => user.Role).Returns(SystemRoleRules.Member);
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

        (await policy.CanAccessProjectAsync(projectId, ownerId, CancellationToken.None)).Should().BeTrue();
        (await policy.CanManageProjectAsync(projectId, ownerId, CancellationToken.None)).Should().BeTrue();
        (await policy.CanAccessTaskAsync(privateTask, CancellationToken.None)).Should().BeFalse();
        (await policy.CanManageTaskAsync(privateTask, CancellationToken.None)).Should().BeFalse();
    }

    [Theory]
    [InlineData(ProjectRoleRules.Customer, false, false, false)]
    [InlineData(ProjectRoleRules.Viewer, false, true, false)]
    [InlineData(ProjectRoleRules.Manager, true, true, true)]
    public async Task CustomRoleUsesInheritedBaseRoleAcrossTaskAndWikiPolicy(
        string baseRole,
        bool canManage,
        bool canReadInternalWiki,
        bool canWriteWiki)
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var roleKey = $"custom-{baseRole.ToLowerInvariant()}";
        _context.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
            new User { Id = memberId, FullName = "Custom role member", Email = $"member-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
        _context.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = "Role policy tenant",
            Code = $"role-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            IsActive = true
        });
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = memberId,
            Role = OrganizationRoleRules.Member
        });
        _context.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Role policy project",
            Code = $"project-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = projectId,
            UserId = memberId,
            Role = roleKey
        });
        _context.ProjectRoleDefinitions.Add(new ProjectRoleDefinition
        {
            OrganizationId = organizationId,
            Key = roleKey,
            DisplayName = $"Custom {baseRole}",
            BaseRole = baseRole,
            CreatedByUserId = ownerId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(memberId);
        currentUser.SetupGet(user => user.Role).Returns(SystemRoleRules.Member);
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

        (await policy.CanManageProjectAsync(projectId, ownerId, CancellationToken.None)).Should().Be(canManage);
        (await policy.CanReadInternalWikiAsync(projectId, ownerId, CancellationToken.None)).Should().Be(canReadInternalWiki);
        (await policy.CanViewProjectWorkloadAsync(projectId, ownerId, CancellationToken.None)).Should().Be(canReadInternalWiki);
        (await policy.CanWriteWikiAsync(projectId, ownerId, CancellationToken.None)).Should().Be(canWriteWiki);
    }

    [Fact]
    public async Task UnknownCustomRoleKeepsMembershipReadBoundaryButFailsClosedForWrites()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
            new User { Id = memberId, FullName = "Unknown role", Email = $"unknown-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Legacy project", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = memberId, Role = "missing-role" });
        await _context.SaveChangesAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(memberId);
        currentUser.SetupGet(user => user.Role).Returns(SystemRoleRules.Member);
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

        (await policy.CanAccessProjectAsync(projectId, ownerId, CancellationToken.None)).Should().BeTrue();
        (await policy.CanManageProjectAsync(projectId, ownerId, CancellationToken.None)).Should().BeFalse();
        (await policy.CanWriteWikiAsync(projectId, ownerId, CancellationToken.None)).Should().BeFalse();
        (await policy.CanReadInternalWikiAsync(projectId, ownerId, CancellationToken.None)).Should().BeFalse();
        (await policy.CanViewProjectWorkloadAsync(projectId, ownerId, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task ReadOnlyViewerAssignedToTask_CannotContributeOrManage()
    {
        var ownerId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        _context.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = $"owner-{Guid.NewGuid():N}@qaly.dev", IsActive = true },
            new User { Id = viewerId, FullName = "Viewer", Email = $"viewer-{Guid.NewGuid():N}@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Read only project", OwnerId = ownerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = viewerId, Role = ProjectRoleRules.Viewer });
        _context.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            AssigneeId = viewerId,
            Title = "Historical assignment"
        });
        await _context.SaveChangesAsync();
        var task = await _context.TaskItems
            .Include(item => item.Project)
            .Include(item => item.Assignees)
            .SingleAsync(item => item.Id == taskId);
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(user => user.UserId).Returns(viewerId);
        currentUser.SetupGet(user => user.Role).Returns(SystemRoleRules.Member);
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

        (await policy.CanAccessTaskAsync(task, CancellationToken.None)).Should().BeTrue();
        (await policy.CanContributeToTaskAsync(task, CancellationToken.None)).Should().BeFalse();
        (await policy.CanManageTaskAsync(task, CancellationToken.None)).Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
