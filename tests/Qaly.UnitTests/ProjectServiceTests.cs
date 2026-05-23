using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class ProjectServiceTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly GenericRepository<Project> _projectRepo;
    private readonly GenericRepository<Organization> _organizationRepo;
    private readonly GenericRepository<OrganizationMember> _organizationMemberRepo;
    private readonly GenericRepository<ProjectMember> _memberRepo;
    private readonly GenericRepository<User> _userRepo;
    private readonly GenericRepository<ProjectLabel> _labelRepo;
    private readonly GenericRepository<VectorSyncOutbox> _outboxRepo;
    private readonly UnitOfWork _uow;
    private readonly Mock<ICurrentUserService> _currentUser;
    private readonly Mock<INotificationService> _notification;
    private readonly Mock<IAuditLogService> _audit;

    public ProjectServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new QalyDbContext(options);
        _projectRepo = new GenericRepository<Project>(_context);
        _organizationRepo = new GenericRepository<Organization>(_context);
        _organizationMemberRepo = new GenericRepository<OrganizationMember>(_context);
        _memberRepo = new GenericRepository<ProjectMember>(_context);
        _userRepo = new GenericRepository<User>(_context);
        _labelRepo = new GenericRepository<ProjectLabel>(_context);
        _outboxRepo = new GenericRepository<VectorSyncOutbox>(_context);
        _uow = new UnitOfWork(_context);

        _currentUser = new Mock<ICurrentUserService>();
        _notification = new Mock<INotificationService>();
        _audit = new Mock<IAuditLogService>();
    }

    private ProjectService CreateService()
    {
        return new ProjectService(
            _projectRepo,
            _organizationRepo,
            _organizationMemberRepo,
            _memberRepo,
            _userRepo,
            _labelRepo,
            _outboxRepo,
            _uow,
            _currentUser.Object,
            _notification.Object,
            _audit.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _userRepo.AddAsync(new User { Id = userId, FullName = "Owner", Email = "owner@qaly.dev", IsActive = true });
        await _context.SaveChangesAsync();
        
        _currentUser.Setup(u => u.UserId).Returns(userId);
        var service = CreateService();
        var dto = new CreateProjectDto("New Project", "PROJ-1", "Description", null, null, null);

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Name.Should().Be("New Project");
        result.Data.Code.Should().Be("proj-1");
        result.Data.OwnerId.Should().Be(userId);

        (await _projectRepo.CountAsync()).Should().Be(1);
        (await _memberRepo.CountAsync()).Should().Be(1); // Owner added as member
    }

    [Fact]
    public async Task CreateAsync_WithEmptyName_ReturnsFailure()
    {
        // Arrange
        _currentUser.Setup(u => u.UserId).Returns(Guid.NewGuid());
        var service = CreateService();
        var dto = new CreateProjectDto("", null, null, null, null, null);

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenProjectExistsAndUserHasAccess_ReturnsProject()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(userId);
        
        var owner = new User { Id = userId, FullName = "Owner User", Email = "owner@qaly.dev" };
        var project = new Project { Id = Guid.NewGuid(), Name = "Existing Project", Code = "ex-1", OwnerId = userId, Owner = owner };
        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();
        
        var service = CreateService();

        // Act
        var result = await service.GetByIdAsync(project.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.Id.Should().Be(project.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserHasNoAccess_ReturnsForbidden()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(otherUserId);
        
        var owner = new User { Id = ownerId, FullName = "Owner User", Email = "owner@qaly.dev" };
        var project = new Project { Id = Guid.NewGuid(), Name = "Private Project", Code = "priv", OwnerId = ownerId, Owner = owner };
        await _projectRepo.AddAsync(project);
        await _context.SaveChangesAsync();
        
        var service = CreateService();

        // Act
        var result = await service.GetByIdAsync(project.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AddMemberAsync_WhenUserIsOwner_AddsMemberSuccessfully()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);
        
        var project = new Project { Id = Guid.NewGuid(), Name = "Project", OwnerId = ownerId };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "member@qaly.dev", FullName = "Member" });
        await _context.SaveChangesAsync();
        
        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, newMemberId, "Member");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // After adding, we have the new member. The owner was NOT added in this test setup manually.
        (await _memberRepo.CountAsync()).Should().Be(1);
        _notification.Verify(n => n.CreateAsync(
            newMemberId,
            It.IsAny<string>(),
            "ProjectInvite",
            "success",
            project.Id,
            nameof(Project),
            $"project:{project.Id}:invite:{newMemberId}",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
#pragma warning restore CA1707
