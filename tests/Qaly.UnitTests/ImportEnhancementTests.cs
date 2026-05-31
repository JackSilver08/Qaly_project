using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Import;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
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
    private readonly Mock<ITaskAccessPolicy> _taskAccessPolicy = new();

    public ImportEnhancementTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
        _taskAccessPolicy
            .Setup(policy => policy.CanManageProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _taskAccessPolicy
            .Setup(policy => policy.CanAccessProjectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

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
        result.Data.FailedCount.Should().Be(1);
        result.Data.DuplicateSkippedCount.Should().Be(0);
        result.Data.SkippedRows.Should().ContainSingle(row => row.RowIndex == 3);
        result.Data.SkippedRows.Single().Category.Should().Be("Failed");
        var task = await _context.TaskItems.SingleAsync();
        task.AssigneeId.Should().Be(assigneeId);
        task.Priority.Should().Be("Critical");
    }

    [Fact]
    public async Task ExecuteImportAsync_WhenFirstRowIsNotHeader_ImportsFirstCsvRow()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        _context.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = importerId, Role = "Owner" });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("First task,High\nSecond task,Low\n"));

        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings:
            [
                new ColumnMapping(0, "Title"),
                new ColumnMapping(1, "Priority")
            ],
            FirstRowIsHeader: false,
            SkipDuplicates: false,
            SheetName: null,
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.ImportedCount.Should().Be(2);
        (await _context.TaskItems.Select(task => task.Title).ToListAsync())
            .Should().BeEquivalentTo("First task", "Second task");
    }

    [Fact]
    public async Task ParseFileAsync_WhenFirstRowIsNotHeader_KeepsFirstRowInPreview()
    {
        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("First task,High\nSecond task,Low\n"));

        var result = await service.ParseFileAsync(stream, "tasks.csv", firstRowIsHeader: false);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Headers.Should().BeEquivalentTo("Column 1", "Column 2");
        result.Data.PreviewRows.Should().HaveCount(2);
        result.Data.PreviewRows[0][0].Should().Be("First task");
        result.Data.Suggestions.Should().ContainSingle(s => s.ColumnIndex == 0 && s.SuggestedField == "Title");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithJsonObjectArray_ImportsTasks()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            """[{"Title":"JSON task","Priority":"High","Status":"Done"},{"Title":"Second JSON task","Priority":"Low"}]"""));

        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings:
            [
                new ColumnMapping(0, "Title"),
                new ColumnMapping(1, "Priority"),
                new ColumnMapping(2, "Status")
            ],
            FirstRowIsHeader: true,
            SkipDuplicates: false,
            SheetName: null,
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.json", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.ImportedCount.Should().Be(2);
        (await _context.TaskItems.Select(task => task.Title).ToListAsync())
            .Should().BeEquivalentTo("JSON task", "Second JSON task");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithSemicolonTxt_AutoDetectsDelimiter()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title;Priority\nTXT task;Critical\n"));

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
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.txt", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        var task = await _context.TaskItems.SingleAsync();
        task.Title.Should().Be("TXT task");
        task.Priority.Should().Be("Critical");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithDsv_AutoDetectsDelimiter()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title;Priority\nDSV task;High\n"));

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
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.dsv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        var task = await _context.TaskItems.SingleAsync();
        task.Title.Should().Be("DSV task");
        task.Priority.Should().Be("High");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithSkipDuplicates_SplitsDuplicateSkippedCount()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        _context.TaskItems.Add(new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, ReporterId = importerId, Title = "Existing task", Status = "Todo" });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title\nExisting task\nNew task\n"));

        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings: [new ColumnMapping(0, "Title")],
            FirstRowIsHeader: true,
            SkipDuplicates: true,
            SheetName: null,
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.ImportedCount.Should().Be(1);
        result.Data.FailedCount.Should().Be(0);
        result.Data.DuplicateSkippedCount.Should().Be(1);
        result.Data.SkippedRows.Should().ContainSingle(row => row.Category == "Duplicate");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithDuplicateMappedFields_ReturnsValidationError()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title,Name\nTask A,Task B\n"));

        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings:
            [
                new ColumnMapping(0, "Title"),
                new ColumnMapping(1, "Title")
            ],
            FirstRowIsHeader: true,
            SkipDuplicates: false,
            SheetName: null,
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        (await _context.TaskItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteImportAsync_WhenUserCannotManageProject_ReturnsForbidden()
    {
        var importerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);
        _taskAccessPolicy
            .Setup(policy => policy.CanManageProjectAsync(projectId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Users.Add(new User { Id = ownerId, FullName = "Owner", Email = "owner@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = ownerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title\nTask A\n"));
        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings: [new ColumnMapping(0, "Title")],
            FirstRowIsHeader: true,
            SkipDuplicates: false,
            SheetName: null,
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await _context.TaskItems.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteImportAsync_AppendsTasksUsingKanbanSortOrderSpacing()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        _context.TaskItems.AddRange(
            new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, ReporterId = importerId, Title = "Existing 1", Status = "Todo", SortOrder = 1000 },
            new TaskItem { Id = Guid.NewGuid(), ProjectId = projectId, ReporterId = importerId, Title = "Existing 2", Status = "Todo", SortOrder = 2000 });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title,Status\nImported 1,Todo\nImported 2,Todo\n"));
        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings:
            [
                new ColumnMapping(0, "Title"),
                new ColumnMapping(1, "Status")
            ],
            FirstRowIsHeader: true,
            SkipDuplicates: false,
            SheetName: null,
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        var importedSortOrders = await _context.TaskItems
            .Where(task => task.Title.StartsWith("Imported"))
            .OrderBy(task => task.Title)
            .Select(task => task.SortOrder)
            .ToListAsync();
        importedSortOrders.Should().Equal(3000, 4000);
    }

    [Fact]
    public async Task ExecuteImportAsync_WhenCreatingProject_UsesUniqueGeneratedProjectCode()
    {
        var importerId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.AddRange(
            new Project { Id = Guid.NewGuid(), Name = "Quality Platform", Code = "QP", OwnerId = importerId },
            new Project { Id = Guid.NewGuid(), Name = "Quality Platform 2", Code = "QP-2", OwnerId = importerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title\nImported task\n"));
        var request = new ImportRequest(
            ProjectId: null,
            NewProjectName: "Quality Platform",
            Mappings: [new ColumnMapping(0, "Title")],
            FirstRowIsHeader: true,
            SkipDuplicates: false,
            SheetName: null,
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        var project = await _context.Projects.SingleAsync(project => project.Id == result.Data!.ProjectId);
        project.Code.Should().Be("QP-3");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithAliasDefaultPriority_NormalizesPriority()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title,Priority\nTask A,\n"));
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
            DefaultPriority: "urgent",
            EnableAiCategorization: false);

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        var task = await _context.TaskItems.SingleAsync();
        task.Priority.Should().Be("Critical");
    }

    [Fact]
    public async Task ExecuteImportAsync_WithDefaultStatus_UsesTargetKanbanColumnWhenStatusIsEmpty()
    {
        var importerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _currentUser.SetupGet(user => user.UserId).Returns(importerId);

        _context.Users.Add(new User { Id = importerId, FullName = "PM", Email = "pm@qaly.dev", IsActive = true });
        _context.Projects.Add(new Project { Id = projectId, Name = "Project", Code = "PRJ", OwnerId = importerId });
        await _context.SaveChangesAsync();

        var service = CreateService();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Title,Status\nTask A,\n"));
        var request = new ImportRequest(
            ProjectId: projectId,
            NewProjectName: null,
            Mappings:
            [
                new ColumnMapping(0, "Title"),
                new ColumnMapping(1, "Status")
            ],
            FirstRowIsHeader: true,
            SkipDuplicates: false,
            SheetName: null,
            EnableAiCategorization: false,
            DefaultStatus: "InReview");

        var result = await service.ExecuteImportAsync(stream, "tasks.csv", request);

        result.IsSuccess.Should().BeTrue(result.Error);
        var task = await _context.TaskItems.SingleAsync();
        task.Status.Should().Be("InReview");
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
            Mock.Of<IAiService>(),
            _taskAccessPolicy.Object);
}
#pragma warning restore CA1707
