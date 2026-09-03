using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class ProjectRoleDefinitionServiceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly GenericRepository<ProjectRoleDefinition> _definitions;
    private readonly ProjectRoleCatalog _catalog;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    public ProjectRoleDefinitionServiceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _definitions = new GenericRepository<ProjectRoleDefinition>(_db);
        _catalog = new ProjectRoleCatalog(_definitions);

        _db.Users.Add(new User { Id = _ownerId, FullName = "Owner", Email = "owner@qaly.dev", IsActive = true });
        _db.Organizations.Add(new Organization
        {
            Id = _organizationId,
            OwnerId = _ownerId,
            Name = "Qaly",
            Code = $"qaly-{Guid.NewGuid():N}"
        });
        _db.SaveChanges();
        _currentUser.SetupGet(service => service.UserId).Returns(_ownerId);
    }

    [Fact]
    public async Task Owner_CanCreateAndEditRole_WithServerDerivedPermissionPreview()
    {
        var service = CreateService();

        var created = await service.CreateAsync(_organizationId,
            new CreateProjectRoleDefinitionDto("Backend Engineer", ProjectRoleRules.Developer, "API", "dotnet, sql"));

        created.StatusCode.Should().Be(201);
        created.Data!.Key.Should().Be("backend-engineer");
        created.Data.PermissionSummary.Should().NotBeNullOrWhiteSpace();
        created.Data.AiTier.Should().Be(AiCapabilityTier.Specialist.ToString());
        created.Data.SkillTags.Should().BeEquivalentTo("dotnet", "sql");

        var updated = await service.UpdateAsync(created.Data.Id,
            new UpdateProjectRoleDefinitionDto("Senior Backend Engineer", ProjectRoleRules.Tester, "Quality", "test", true));

        updated.IsSuccess.Should().BeTrue(updated.Error);
        updated.Data!.Key.Should().Be("backend-engineer", "stable keys prevent orphaned memberships");
        updated.Data.DisplayName.Should().Be("Senior Backend Engineer");
        updated.Data.BaseRole.Should().Be(ProjectRoleRules.Tester);
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Owner")]
    public async Task Create_RejectsBuiltInRoleSpoof(string displayName)
    {
        var result = await CreateService().CreateAsync(_organizationId,
            new CreateProjectRoleDefinitionDto(displayName, ProjectRoleRules.Member));

        result.StatusCode.Should().Be(400);
        (await _db.ProjectRoleDefinitions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task OrganizationMember_CannotMutateRoleDefinitions()
    {
        var memberId = Guid.NewGuid();
        _db.Users.Add(new User { Id = memberId, FullName = "Member", Email = "member@qaly.dev", IsActive = true });
        _db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = _organizationId,
            UserId = memberId,
            Role = OrganizationRoleRules.Member
        });
        await _db.SaveChangesAsync();
        _currentUser.SetupGet(service => service.UserId).Returns(memberId);

        var result = await CreateService().CreateAsync(_organizationId,
            new CreateProjectRoleDefinitionDto("Backend Engineer", ProjectRoleRules.Developer));

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DeactivatedRole_StillResolvesExistingMembership_ButCannotBeAssignedAgain()
    {
        var definition = new ProjectRoleDefinition
        {
            OrganizationId = _organizationId,
            CreatedByUserId = _ownerId,
            Key = "backend-engineer",
            DisplayName = "Backend Engineer",
            BaseRole = ProjectRoleRules.Developer,
            IsActive = false
        };
        await _definitions.AddAsync(definition);
        await _db.SaveChangesAsync();

        var existing = await _catalog.ResolveAsync(definition.Key, _organizationId);
        var assignable = await _catalog.ResolveAssignableAsync(definition.Key, _organizationId);

        existing.Should().NotBeNull();
        existing!.BaseRole.Should().Be(ProjectRoleRules.Developer);
        assignable.Should().BeNull();
    }

    private ProjectRoleDefinitionService CreateService()
        => new(
            _definitions,
            new GenericRepository<Organization>(_db),
            new GenericRepository<OrganizationMember>(_db),
            new GenericRepository<Project>(_db),
            new GenericRepository<ProjectMember>(_db),
            _catalog,
            new UnitOfWork(_db),
            _currentUser.Object,
            _audit.Object,
            new TaskAccessPolicy(
                _currentUser.Object,
                new GenericRepository<Project>(_db),
                new GenericRepository<ProjectMember>(_db),
                new GenericRepository<OrganizationMember>(_db),
                _catalog));

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
