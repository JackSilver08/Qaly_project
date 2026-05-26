using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public class SoftDeleteQueryFilterTests : IDisposable
{
    private readonly QalyDbContext _context;

    public SoftDeleteQueryFilterTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task RepositoryDeleteAsync_SoftDeletesAndGlobalFilterHidesEntity()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.Users.Add(new User { Id = ownerId, FullName = "Owner", Email = "owner@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Archived Project", Code = "archived-project", OwnerId = ownerId });
        await _context.SaveChangesAsync();

        var repository = new GenericRepository<Project>(_context);
        var project = await repository.GetByIdAsync(projectId);

        await repository.DeleteAsync(project!);
        await _context.SaveChangesAsync();

        (await _context.Projects.CountAsync()).Should().Be(0);

        var softDeleted = await _context.Projects.IgnoreQueryFilters().SingleAsync();
        softDeleted.IsDeleted.Should().BeTrue();
        softDeleted.DeletedAt.Should().NotBeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
