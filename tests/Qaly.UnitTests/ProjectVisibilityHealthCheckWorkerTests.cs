using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class ProjectVisibilityHealthCheckWorkerTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly QalyDbContext _db;
    private readonly Guid _userId = Guid.NewGuid();

    public ProjectVisibilityHealthCheckWorkerTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _db.Users.Add(new User
        {
            Id = _userId,
            FullName = "Visibility owner",
            Email = "visibility-owner@qaly.test",
            PasswordHash = "test"
        });
        _db.SaveChanges();
        _provider = new ServiceCollection().AddSingleton(_db).BuildServiceProvider();
    }

    [Fact]
    public async Task RunHealthCheckAsync_DoesNotTreatTrashedProjectMembershipAsOrphan()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Restorable Project",
            Code = "RESTORE",
            OwnerId = _userId,
            IsDeleted = true,
            DeletedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);
        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = _userId,
            Role = "Owner"
        });
        await _db.SaveChangesAsync();

        await CreateWorker().RunHealthCheckAsync(CancellationToken.None);

        (await _db.AuditLogs.CountAsync(log => log.Action == "VisibilityHealthCheckAlert"))
            .Should().Be(0);
    }

    [Fact]
    public async Task RunHealthCheckAsync_DeduplicatesRepeatedOrphanAlertWithinCooldown()
    {
        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = Guid.NewGuid(),
            UserId = _userId,
            Role = "Member"
        });
        await _db.SaveChangesAsync();

        var worker = CreateWorker();
        await worker.RunHealthCheckAsync(CancellationToken.None);
        await worker.RunHealthCheckAsync(CancellationToken.None);

        (await _db.AuditLogs.CountAsync(log => log.Action == "VisibilityHealthCheckAlert"))
            .Should().Be(1);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProjectVisibilityHealthCheckWorker CreateWorker()
        => new(_provider, NullLogger<ProjectVisibilityHealthCheckWorker>.Instance);
}
