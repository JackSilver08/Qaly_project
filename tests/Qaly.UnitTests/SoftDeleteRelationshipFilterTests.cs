using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;

namespace Qaly.UnitTests;

public sealed class SoftDeleteRelationshipFilterTests : IDisposable
{
    private readonly QalyDbContext _context;

    public SoftDeleteRelationshipFilterTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task RequiredDependents_AreHiddenWithSoftDeletedParents()
    {
        var owner = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Owner",
            Email = "owner-filter@qaly.dev"
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Hidden project",
            Code = "HID",
            OwnerId = owner.Id
        };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            ReporterId = owner.Id,
            Title = "Hidden task",
            IsDeleted = true
        };
        var timeEntry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            UserId = owner.Id,
            StartedAt = DateTimeOffset.UtcNow,
            ManualMinutes = 15
        };
        var group = new WorkGroup
        {
            Id = Guid.NewGuid(),
            Name = "Hidden group",
            OwnerId = owner.Id,
            IsDeleted = true
        };
        var membership = new WorkGroupMember
        {
            WorkGroupId = group.Id,
            UserId = owner.Id,
            Role = "Owner"
        };
        _context.AddRange(owner, project, task, timeEntry, group, membership);
        await _context.SaveChangesAsync();

        (await _context.TimeEntries.ToListAsync()).Should().BeEmpty();
        (await _context.WorkGroupMembers.ToListAsync()).Should().BeEmpty();
        (await _context.TimeEntries.IgnoreQueryFilters().ToListAsync()).Should().ContainSingle();
        (await _context.WorkGroupMembers.IgnoreQueryFilters().ToListAsync()).Should().ContainSingle();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
