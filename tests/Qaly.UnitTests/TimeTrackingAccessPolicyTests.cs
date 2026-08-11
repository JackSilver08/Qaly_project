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

public sealed class TimeTrackingAccessPolicyTests : IDisposable
{
    private readonly QalyDbContext _context;

    public TimeTrackingAccessPolicyTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task GetByProjectAsync_ProjectAndOrganizationMember_ExcludesUnrelatedPrivateTaskEntries()
    {
        var owner = User("owner");
        var member = User("member");
        var organization = new Organization
        {
            Name = "Delivery",
            Code = "DEL",
            OwnerId = owner.Id
        };
        var project = Project(owner.Id, organization.Id);
        var publicTask = Task(project.Id, owner.Id, "Public");
        var privateTask = Task(project.Id, owner.Id, "Private", isPrivate: true);
        _context.AddRange(owner, member, organization, project, publicTask, privateTask);
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = member.Id,
            Role = OrganizationRoleRules.Member
        });
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = member.Id,
            Role = ProjectRoleRules.Member
        });
        _context.TimeEntries.AddRange(
            Entry(publicTask.Id, owner.Id),
            Entry(privateTask.Id, owner.Id));
        await _context.SaveChangesAsync();

        var result = await CreateService(member).GetByProjectAsync(project.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data.Should().ContainSingle(item => item.TaskId == publicTask.Id);
    }

    [Fact]
    public async Task GetByTaskAsync_FormerMemberWithHistoricalAssignment_IsForbidden()
    {
        var owner = User("owner");
        var formerMember = User("former");
        var project = Project(owner.Id);
        var task = Task(project.Id, owner.Id, "Historical assignment");
        _context.AddRange(owner, formerMember, project, task);
        _context.TaskAssignments.Add(new TaskAssignment
        {
            TaskItemId = task.Id,
            UserId = formerMember.Id
        });
        await _context.SaveChangesAsync();

        var result = await CreateService(formerMember).GetByTaskAsync(task.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetByProjectAsync_OwnerOfProjectWithoutTasks_ReturnsRealEmptyResult()
    {
        var owner = User("owner");
        var project = Project(owner.Id);
        _context.AddRange(owner, project);
        await _context.SaveChangesAsync();

        var result = await CreateService(owner).GetByProjectAsync(project.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data.Should().BeEmpty();
    }

    private TimeTrackingService CreateService(User currentUser)
    {
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(service => service.UserId).Returns(currentUser.Id);
        current.SetupGet(service => service.UserName).Returns(currentUser.FullName);
        current.SetupGet(service => service.Role).Returns(currentUser.Role);

        var projectRepo = new GenericRepository<Project>(_context);
        var taskRepo = new GenericRepository<TaskItem>(_context);
        var accessPolicy = new TaskAccessPolicy(
            current.Object,
            projectRepo,
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context));

        return new TimeTrackingService(
            new GenericRepository<TimeEntry>(_context),
            taskRepo,
            projectRepo,
            new UnitOfWork(_context),
            current.Object,
            accessPolicy);
    }

    private static User User(string key) => new()
    {
        Id = Guid.NewGuid(),
        FullName = key,
        Email = $"{key}@qaly.dev",
        Role = ProjectRoleRules.Member,
        IsActive = true
    };

    private static Project Project(Guid ownerId, Guid? organizationId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Project",
        Code = $"P-{Guid.NewGuid():N}"[..10],
        OwnerId = ownerId,
        OrganizationId = organizationId
    };

    private static TaskItem Task(Guid projectId, Guid reporterId, string title, bool isPrivate = false) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        ReporterId = reporterId,
        Title = title,
        IsPrivate = isPrivate
    };

    private static TimeEntry Entry(Guid taskId, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        TaskId = taskId,
        UserId = userId,
        StartedAt = DateTimeOffset.UtcNow,
        ManualMinutes = 30
    };

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
