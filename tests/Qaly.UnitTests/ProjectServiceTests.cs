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
    private readonly GenericRepository<TaskAttachment> _attachmentRepo;
    private readonly GenericRepository<PhysicalFile> _physicalFileRepo;
    private readonly GenericRepository<VectorSyncOutbox> _outboxRepo;
    private readonly GenericRepository<ProjectRoleDefinition> _roleDefinitionRepo;
    private readonly ProjectRoleCatalog _roleCatalog;
    private readonly UnitOfWork _uow;
    private readonly Mock<IFileStorageService> _fileStorage;
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
        _attachmentRepo = new GenericRepository<TaskAttachment>(_context);
        _physicalFileRepo = new GenericRepository<PhysicalFile>(_context);
        _outboxRepo = new GenericRepository<VectorSyncOutbox>(_context);
        _roleDefinitionRepo = new GenericRepository<ProjectRoleDefinition>(_context);
        _roleCatalog = new ProjectRoleCatalog(_roleDefinitionRepo);
        _uow = new UnitOfWork(_context);

        _fileStorage = new Mock<IFileStorageService>();
        _currentUser = new Mock<ICurrentUserService>();
        _notification = new Mock<INotificationService>();
        _audit = new Mock<IAuditLogService>();
    }

    private ProjectService CreateService(IUnitOfWork? unitOfWork = null)
    {
        return new ProjectService(
            _projectRepo,
            _organizationRepo,
            _organizationMemberRepo,
            _memberRepo,
            _userRepo,
            _labelRepo,
            _attachmentRepo,
            _physicalFileRepo,
            _outboxRepo,
            _roleDefinitionRepo,
            _roleCatalog,
            unitOfWork ?? _uow,
            _fileStorage.Object,
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
    public async Task CreateAsync_WhenAuditStagingFails_DoesNotCommitProjectGraphOrOutbox()
    {
        var userId = Guid.NewGuid();
        await _userRepo.AddAsync(new User
        {
            Id = userId,
            FullName = "Owner",
            Email = "project-audit-failure@qaly.dev",
            IsActive = true
        });
        await _context.SaveChangesAsync();
        _currentUser.Setup(service => service.UserId).Returns(userId);
        _audit
            .Setup(service => service.StageAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("simulated audit staging failure"));
        var unitOfWork = new CountingUnitOfWork(_context);
        var service = CreateService(unitOfWork);

        var action = () => service.CreateAsync(new CreateProjectDto(
            "Must Roll Back",
            "rollback-project",
            null,
            null,
            null,
            null));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated audit staging failure");
        unitOfWork.SaveCount.Should().Be(0);

        _context.ChangeTracker.Clear();
        (await _context.Projects.CountAsync()).Should().Be(0);
        (await _context.ProjectMembers.CountAsync()).Should().Be(0);
        (await _context.VectorSyncOutbox.CountAsync()).Should().Be(0);
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
        await _userRepo.AddAsync(new User
        {
            Id = ownerId,
            IsActive = true,
            Email = "owner@qaly.dev",
            FullName = "Owner"
        });
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

    [Fact]
    public async Task AddMemberAsync_WhenProjectBelongsToOrganization_GrantsOrganizationMembership()
    {
        // Arrange: project reads are filtered by organization membership, so a project member
        // without an organization row would see an empty project list.
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Qaly",
            Code = "qaly",
            OwnerId = ownerId,
            IsActive = true
        };
        await _organizationRepo.AddAsync(organization);
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = ownerId,
            Role = OrganizationRoleRules.Owner
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            OwnerId = ownerId,
            OrganizationId = organization.Id
        };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "member@qaly.dev", FullName = "Member" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, newMemberId, "Member");

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);

        var organizationMembership = await _organizationMemberRepo.GetQueryable()
            .SingleOrDefaultAsync(member => member.OrganizationId == organization.Id && member.UserId == newMemberId);
        organizationMembership.Should().NotBeNull("a project member must be able to read the project");
        organizationMembership!.Role.Should().Be(OrganizationRoleRules.Member);

        // The new member can now actually see the project.
        _currentUser.Setup(u => u.UserId).Returns(newMemberId);
        var visible = await CreateService().GetAllAsync();
        visible.IsSuccess.Should().BeTrue();
        visible.Data!.Items.Should().ContainSingle(item => item.Id == project.Id);
    }

    [Fact]
    public async Task AddMemberAsync_WhenUserAlreadyHasElevatedOrganizationRole_DoesNotDowngradeIt()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var existingAdminId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Qaly",
            Code = "qaly",
            OwnerId = ownerId,
            IsActive = true
        };
        await _organizationRepo.AddAsync(organization);
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = existingAdminId,
            Role = OrganizationRoleRules.OrganizationAdmin
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            OwnerId = ownerId,
            OrganizationId = organization.Id
        };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = existingAdminId, IsActive = true, Email = "orgadmin@qaly.dev", FullName = "Org Admin" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, existingAdminId, "Member");

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);
        var memberships = await _organizationMemberRepo.GetQueryable()
            .Where(member => member.OrganizationId == organization.Id && member.UserId == existingAdminId)
            .ToListAsync();
        memberships.Should().ContainSingle();
        memberships[0].Role.Should().Be(OrganizationRoleRules.OrganizationAdmin);
    }

    [Fact]
    public async Task AddMemberAsync_WhenProjectHasNoOrganization_DoesNotCreateOrganizationMembership()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);

        var project = new Project { Id = Guid.NewGuid(), Name = "Personal Project", OwnerId = ownerId };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "member@qaly.dev", FullName = "Member" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, newMemberId, "Member");

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);
        (await _organizationMemberRepo.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddMemberAsync_WhenCallerIsPlainMember_IsStillForbidden()
    {
        // Arrange: the backfill must not become a way for a non-manager to join people to an organization.
        var ownerId = Guid.NewGuid();
        var plainMemberId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(plainMemberId);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Qaly",
            Code = "qaly",
            OwnerId = ownerId,
            IsActive = true
        };
        await _organizationRepo.AddAsync(organization);
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = plainMemberId,
            Role = OrganizationRoleRules.Member
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            OwnerId = ownerId,
            OrganizationId = organization.Id
        };
        await _projectRepo.AddAsync(project);
        await _memberRepo.AddAsync(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = plainMemberId,
            Role = ProjectRoleRules.Member
        });
        await _userRepo.AddAsync(new User { Id = plainMemberId, IsActive = true, Email = "member@qaly.dev", FullName = "Member" });
        await _userRepo.AddAsync(new User { Id = outsiderId, IsActive = true, Email = "outsider@qaly.dev", FullName = "Outsider" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, outsiderId, "Member");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await _organizationMemberRepo.GetQueryable().AnyAsync(member => member.UserId == outsiderId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task AddMemberAsync_WithUnknownRole_IsRejectedInsteadOfSilentlyBecomingMember()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);

        var project = new Project { Id = Guid.NewGuid(), Name = "Project", OwnerId = ownerId };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "member@qaly.dev", FullName = "Member" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, newMemberId, "Backend Dev");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        (await _memberRepo.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddMemberAsync_WithOrganizationDefinedRole_StoresKeyAndInheritsBaseRolePermissions()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Qaly",
            Code = "qaly",
            OwnerId = ownerId,
            IsActive = true
        };
        await _organizationRepo.AddAsync(organization);
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = ownerId,
            Role = OrganizationRoleRules.Owner
        });
        await _roleDefinitionRepo.AddAsync(new ProjectRoleDefinition
        {
            OrganizationId = organization.Id,
            Key = "dev-backend",
            DisplayName = "Lập trình viên Backend",
            BaseRole = ProjectRoleRules.Developer,
            SkillTags = "backend,dotnet",
            IsActive = true,
            CreatedByUserId = ownerId
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            OwnerId = ownerId,
            OrganizationId = organization.Id
        };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "dev@qaly.dev", FullName = "Dev" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, newMemberId, "dev-backend");

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);
        var membership = await _memberRepo.GetQueryable()
            .SingleAsync(member => member.ProjectId == project.Id && member.UserId == newMemberId);
        membership.Role.Should().Be("dev-backend");

        // The custom role reports its own label but Developer's permissions.
        _currentUser.Setup(u => u.UserId).Returns(newMemberId);
        var projectResult = await CreateService().GetByIdAsync(project.Id);
        projectResult.IsSuccess.Should().BeTrue(projectResult.Error);

        var permissions = projectResult.Data!.Permissions;
        permissions.Should().NotBeNull();
        permissions!.RoleLabel.Should().Be("Lập trình viên Backend");
        permissions.CanCreateTask.Should().BeTrue("Developer may create tasks");
        permissions.CanManageProject.Should().BeFalse("Developer does not manage the project");
        permissions.AiTier.Should().Be(nameof(AiCapabilityTier.Specialist));
    }

    [Fact]
    public async Task AddMemberAsync_WithRoleFromAnotherOrganization_IsRejected()
    {
        // Arrange: a custom role must not leak across organization boundaries.
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);

        var ownOrganization = new Organization
        {
            Id = Guid.NewGuid(), Name = "Own", Code = "own", OwnerId = ownerId, IsActive = true
        };
        var otherOrganization = new Organization
        {
            Id = Guid.NewGuid(), Name = "Other", Code = "other", OwnerId = Guid.NewGuid(), IsActive = true
        };
        await _organizationRepo.AddAsync(ownOrganization);
        await _organizationRepo.AddAsync(otherOrganization);
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = ownOrganization.Id,
            UserId = ownerId,
            Role = OrganizationRoleRules.Owner
        });
        await _roleDefinitionRepo.AddAsync(new ProjectRoleDefinition
        {
            OrganizationId = otherOrganization.Id,
            Key = "dev-backend",
            DisplayName = "Lập trình viên Backend",
            BaseRole = ProjectRoleRules.Developer,
            IsActive = true,
            CreatedByUserId = Guid.NewGuid()
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            OwnerId = ownerId,
            OrganizationId = ownOrganization.Id
        };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "dev@qaly.dev", FullName = "Dev" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, newMemberId, "dev-backend");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        (await _memberRepo.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddMemberAsync_WithDeactivatedCustomRole_IsRejected()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        _currentUser.Setup(u => u.UserId).Returns(ownerId);

        var organization = new Organization
        {
            Id = Guid.NewGuid(), Name = "Qaly", Code = "qaly", OwnerId = ownerId, IsActive = true
        };
        await _organizationRepo.AddAsync(organization);
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = ownerId,
            Role = OrganizationRoleRules.Owner
        });
        await _roleDefinitionRepo.AddAsync(new ProjectRoleDefinition
        {
            OrganizationId = organization.Id,
            Key = "dev-backend",
            DisplayName = "Lập trình viên Backend",
            BaseRole = ProjectRoleRules.Developer,
            IsActive = false,
            CreatedByUserId = ownerId
        });

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            OwnerId = ownerId,
            OrganizationId = organization.Id
        };
        await _projectRepo.AddAsync(project);
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "dev@qaly.dev", FullName = "Dev" });
        await _context.SaveChangesAsync();

        var service = CreateService();

        // Act
        var result = await service.AddMemberAsync(project.Id, newMemberId, "dev-backend");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AddMemberAsync_CustomRoleInheritingManager_CanManageMembership()
    {
        var ownerId = Guid.NewGuid();
        var customManagerId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = "owner-manager@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = customManagerId, IsActive = true, Email = "manager@qaly.dev", FullName = "Manager" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = "new-member@qaly.dev", FullName = "New member" });
        await _organizationRepo.AddAsync(new Organization
        {
            Id = organizationId,
            Name = "Qaly",
            Code = "custom-manager",
            OwnerId = ownerId,
            IsActive = true
        });
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = customManagerId,
            Role = OrganizationRoleRules.Member
        });
        await _roleDefinitionRepo.AddAsync(new ProjectRoleDefinition
        {
            OrganizationId = organizationId,
            Key = "delivery-lead",
            DisplayName = "Delivery Lead",
            BaseRole = ProjectRoleRules.Manager,
            IsActive = true,
            CreatedByUserId = ownerId
        });
        await _projectRepo.AddAsync(new Project
        {
            Id = projectId,
            Name = "Project",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        await _memberRepo.AddAsync(new ProjectMember
        {
            ProjectId = projectId,
            UserId = customManagerId,
            Role = "delivery-lead"
        });
        await _context.SaveChangesAsync();
        _currentUser.SetupGet(user => user.UserId).Returns(customManagerId);

        var result = await CreateService().AddMemberAsync(projectId, newMemberId, ProjectRoleRules.Member);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await _memberRepo.GetQueryable().AnyAsync(member =>
            member.ProjectId == projectId && member.UserId == newMemberId)).Should().BeTrue();
    }

    [Fact]
    public async Task GetByUserAsync_DoesNotTreatOrdinaryOrganizationMembershipAsProjectAccess()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = $"owner-{Guid.NewGuid():N}@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = memberId, IsActive = true, Email = $"member-{Guid.NewGuid():N}@qaly.dev", FullName = "Member" });
        await _organizationRepo.AddAsync(new Organization
        {
            Id = organizationId,
            Name = "Scoped tenant",
            Code = $"scoped-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            IsActive = true
        });
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = memberId,
            Role = OrganizationRoleRules.Member
        });
        await _projectRepo.AddAsync(new Project
        {
            Name = "Not automatically visible",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        await _context.SaveChangesAsync();
        _currentUser.SetupGet(user => user.UserId).Returns(memberId);

        var result = await CreateService().GetByUserAsync(memberId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task OrganizationAdminCanReadAndManagePortfolioProjectWithoutProjectMembership()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var newMemberId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await _userRepo.AddAsync(new User { Id = ownerId, IsActive = true, Email = $"owner-{Guid.NewGuid():N}@qaly.dev", FullName = "Owner" });
        await _userRepo.AddAsync(new User { Id = adminId, IsActive = true, Email = $"admin-{Guid.NewGuid():N}@qaly.dev", FullName = "Organization admin" });
        await _userRepo.AddAsync(new User { Id = newMemberId, IsActive = true, Email = $"new-{Guid.NewGuid():N}@qaly.dev", FullName = "New member" });
        await _organizationRepo.AddAsync(new Organization
        {
            Id = organizationId,
            Name = "Managed tenant",
            Code = $"managed-{Guid.NewGuid():N}",
            OwnerId = ownerId,
            IsActive = true
        });
        await _organizationMemberRepo.AddAsync(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = adminId,
            Role = OrganizationRoleRules.OrganizationAdmin
        });
        await _projectRepo.AddAsync(new Project
        {
            Id = projectId,
            Name = "Portfolio project",
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        await _context.SaveChangesAsync();
        _currentUser.SetupGet(user => user.UserId).Returns(adminId);

        var service = CreateService();
        var project = await service.GetByIdAsync(projectId);
        var addMember = await service.AddMemberAsync(projectId, newMemberId, ProjectRoleRules.Member);

        project.IsSuccess.Should().BeTrue(project.Error);
        var permissions = project.Data!.Permissions!;
        permissions.CanManageProject.Should().BeTrue();
        permissions.Role.Should().Be(OrganizationRoleRules.OrganizationAdmin);
        addMember.IsSuccess.Should().BeTrue(addMember.Error);
    }

    private sealed class CountingUnitOfWork(QalyDbContext context) : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return context.SaveChangesAsync(cancellationToken);
        }

        public void Dispose()
        {
        }
    }
}
#pragma warning restore CA1707
