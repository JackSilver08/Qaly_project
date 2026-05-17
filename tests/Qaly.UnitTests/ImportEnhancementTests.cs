using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Import;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class ImportEnhancementTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public ImportEnhancementTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ExecuteImportAsync_UsesDefaultAssigneeAndReturnsSkippedRows()
    {
        var importerId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Users.Add(new User { Id = assigneeId, FullName = "Dev", Email = "dev@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Dự án", Code = "du-an", OwnerId = importerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = importerId, Role = "Owner" });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = assigneeId, Role = "Developer" });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title,Priority\nTask hợp lệ,\n,High\n"));

        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings:
            [
                new ColumnMapping(0, "Title"),
                new ColumnMapping(1, "Priority")
            ],
            FirstRowIsHeader: true,
            SkipDuplicates: false,
            SheetName: null,
            DefaultAssigneeId: assigneeId,
            AssignToMeIfEmpty: false,
            DefaultPriority: "Critical",
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.ImportedCount.Should().Be(1);
        result.Data.SkippedRows.Should().ContainSingle(row => row.RowIndex == 3);
        var task = await _context.TaskItems.SingleAsync();
        task.AssigneeId.Should().Be(assigneeId);
        task.Priority.Should().Be("Critical");
    }

    private ImportService CreateService()
        => new(
            new GenericRepository<Project>(_context),
            new GenericRepository<TaskItem>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<ProjectLabel>(_context),
            new GenericRepository<TaskLabel>(_context),
            new GenericRepository<ImportSession>(_context),
            _currentUser.Object,
            new UnitOfWork(_context),
            Mock.Of<ILogger<ImportService>>(),
            Mock.Of<IAiService>());
}
#pragma warning restore CA1707
