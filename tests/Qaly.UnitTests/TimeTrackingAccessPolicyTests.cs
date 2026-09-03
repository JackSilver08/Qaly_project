using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Task;
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

    [Theory]
    [InlineData(ProjectRoleRules.Viewer)]
    [InlineData(ProjectRoleRules.Customer)]
    public async Task StartAndManualEntry_ReadOnlyProjectRole_CannotWriteTime(string projectRole)
    {
        var owner = User("owner");
        var readOnlyUser = User($"readonly-{projectRole}");
        var project = Project(owner.Id);
        var task = Task(project.Id, owner.Id, "Visible task");
        _context.AddRange(owner, readOnlyUser, project, task);
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = readOnlyUser.Id,
            Role = projectRole
        });
        await _context.SaveChangesAsync();

        var service = CreateService(readOnlyUser);
        var started = await service.StartTimerAsync(task.Id);
        var manual = await service.AddManualEntryAsync(new CreateTimeEntryDto(
            task.Id,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            ManualMinutes: 30,
            Note: "read-only attempt"));

        started.IsSuccess.Should().BeFalse();
        started.StatusCode.Should().Be(403);
        manual.IsSuccess.Should().BeFalse();
        manual.StatusCode.Should().Be(403);
        (await _context.TimeEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StartAndManualEntry_ProjectMember_CanContributeTime()
    {
        var owner = User("owner");
        var member = User("contributor");
        var project = Project(owner.Id);
        var task = Task(project.Id, owner.Id, "Visible task");
        _context.AddRange(owner, member, project, task);
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = member.Id,
            Role = ProjectRoleRules.Member
        });
        await _context.SaveChangesAsync();

        var service = CreateService(member);
        var started = await service.StartTimerAsync(task.Id);
        var manual = await service.AddManualEntryAsync(new CreateTimeEntryDto(
            task.Id,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            ManualMinutes: 30,
            Note: "valid contribution"));

        started.IsSuccess.Should().BeTrue(started.Error);
        manual.IsSuccess.Should().BeTrue(manual.Error);
        (await _context.TimeEntries.CountAsync()).Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1441)]
    public async Task AddManualEntry_InvalidMinuteRange_IsRejectedWithoutMutation(int minutes)
    {
        var owner = User("owner");
        var project = Project(owner.Id);
        var task = Task(project.Id, owner.Id, "Invalid manual time");
        _context.AddRange(owner, project, task);
        await _context.SaveChangesAsync();

        var result = await CreateService(owner).AddManualEntryAsync(new CreateTimeEntryDto(
            task.Id,
            DateTimeOffset.UtcNow,
            ManualMinutes: minutes));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        (await _context.TimeEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddManualEntry_DoesNotBecomeAnActiveTimerThatStartTimerCloses()
    {
        var owner = User("owner");
        var project = Project(owner.Id);
        var task = Task(project.Id, owner.Id, "Manual then timer");
        _context.AddRange(owner, project, task);
        await _context.SaveChangesAsync();
        var service = CreateService(owner);

        var manual = await service.AddManualEntryAsync(new CreateTimeEntryDto(
            task.Id,
            DateTimeOffset.UtcNow,
            ManualMinutes: 30));
        var timer = await service.StartTimerAsync(task.Id);

        manual.IsSuccess.Should().BeTrue(manual.Error);
        timer.IsSuccess.Should().BeTrue(timer.Error);
        var storedManual = await _context.TimeEntries.AsNoTracking().SingleAsync(entry => entry.Id == manual.Data!.Id);
        storedManual.EndedAt.Should().BeNull("manual duration is canonical and must not be treated as an open timer");
        storedManual.ManualMinutes.Should().Be(30);
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
            new GenericRepository<OrganizationMember>(_context),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context)));

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
