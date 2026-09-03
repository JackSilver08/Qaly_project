using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.DTOs.GitHub;
using Qaly.Application.Services;
using Qaly.Application.Services.GitHub;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Entities.GitHub;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public class GitHubRepositoryConnectionServiceTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly GitHubRepositoryConnectionService _service;

    // Tenant A
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _orgA = Guid.NewGuid();
    private readonly Guid _projectA = Guid.NewGuid();
    private readonly Guid _installationA = Guid.NewGuid();

    // Tenant B
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _orgB = Guid.NewGuid();
    private readonly Guid _installationB = Guid.NewGuid();

    public GitHubRepositoryConnectionServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);

        SeedTenants();

        var guard = new GitHubAccessGuard(
            new GenericRepository<Project>(_context),
            new GenericRepository<ProjectMember>(_context),
            new GenericRepository<OrganizationMember>(_context),
            _currentUser.Object,
            new TaskAccessPolicy(
                _currentUser.Object,
                new GenericRepository<Project>(_context),
                new GenericRepository<ProjectMember>(_context),
                new GenericRepository<OrganizationMember>(_context),
                new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_context))));

        _service = new GitHubRepositoryConnectionService(
            new GenericRepository<GitHubRepositoryConnection>(_context),
            new GenericRepository<GitHubInstallation>(_context),
            guard,
            new UnitOfWork(_context),
            Mock.Of<IGitHubRepositoryProvider>(provider => provider.GetAsync(
                It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CancellationToken>()) ==
                Task.FromResult<SourceRepositoryMetadata?>(new SourceRepositoryMetadata(
                    5001, "org-a", "web", "org-a/web", "main", true))));
    }

    private void SeedTenants()
    {
        _context.Organizations.AddRange(
            new Organization { Id = _orgA, Name = "Org A", Code = "org-a", OwnerId = _userA, IsActive = true },
            new Organization { Id = _orgB, Name = "Org B", Code = "org-b", OwnerId = _userB, IsActive = true });

        _context.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = _orgA, UserId = _userA, Role = "Owner" },
            new OrganizationMember { OrganizationId = _orgB, UserId = _userB, Role = "Owner" });

        _context.Projects.Add(new Project
        {
            Id = _projectA,
            Name = "Project A",
            Code = "proj-a",
            OwnerId = _userA,
            OrganizationId = _orgA
        });

        _context.GitHubInstallations.AddRange(
            new GitHubInstallation
            {
                Id = _installationA,
                OrganizationId = _orgA,
                InstallationId = 1001,
                AccountId = 2001,
                AccountLogin = "org-a",
                AccountType = "Organization",
                InstalledByUserId = _userA,
                Status = "Active"
            },
            new GitHubInstallation
            {
                Id = _installationB,
                OrganizationId = _orgB,
                InstallationId = 1002,
                AccountId = 2002,
                AccountLogin = "org-b",
                AccountType = "Organization",
                InstalledByUserId = _userB,
                Status = "Active"
            });

        _context.SaveChanges();
    }

    private void ActAs(Guid userId, string role = "Member")
    {
        _currentUser.Setup(x => x.UserId).Returns(userId);
        _currentUser.Setup(x => x.Role).Returns(role);
    }

    private static CreateGitHubRepositoryConnectionDto NewRepoDto(Guid installationId, long externalId = 5001)
        => new(installationId, externalId, "org-a", "web", DefaultBranch: "main", IsPrivate: true);

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateAsync_AsOrgOwner_LinksRepository()
    {
        ActAs(_userA);

        var result = await _service.CreateAsync(_projectA, NewRepoDto(_installationA));

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.OrganizationId.Should().Be(_orgA);
        result.Data!.FullName.Should().Be("org-a/web");
    }

    [Fact]
    public async Task GetByProjectAsync_FromOtherTenant_IsForbidden()
    {
        // Tenant A links a repo.
        ActAs(_userA);
        (await _service.CreateAsync(_projectA, NewRepoDto(_installationA))).IsSuccess.Should().BeTrue();

        // A user from tenant B (no membership of org A) must not read it.
        ActAs(_userB);
        var result = await _service.GetByProjectAsync(_projectA);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateAsync_WithInstallationFromOtherTenant_Fails()
    {
        // Tenant A owner tries to attach tenant B's installation → rejected.
        ActAs(_userA);

        var result = await _service.CreateAsync(_projectA, NewRepoDto(_installationB));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RemoveAsync_FromOtherTenant_DoesNotDelete()
    {
        ActAs(_userA);
        var created = await _service.CreateAsync(_projectA, NewRepoDto(_installationA));
        var connectionId = created.Data!.Id;

        // Tenant B cannot remove tenant A's connection.
        ActAs(_userB);
        var forbidden = await _service.RemoveAsync(_projectA, connectionId);
        forbidden.IsSuccess.Should().BeFalse();
        forbidden.StatusCode.Should().Be(403);

        // Connection still exists for tenant A.
        ActAs(_userA);
        var stillThere = await _service.GetByProjectAsync(_projectA);
        stillThere.Data.Should().ContainSingle(c => c.Id == connectionId);
    }

    [Fact]
    public async Task CreateAsync_DuplicateRepository_ReturnsConflict()
    {
        ActAs(_userA);
        (await _service.CreateAsync(_projectA, NewRepoDto(_installationA, 7777))).IsSuccess.Should().BeTrue();

        var duplicate = await _service.CreateAsync(_projectA, NewRepoDto(_installationA, 7777));

        duplicate.IsSuccess.Should().BeFalse();
        duplicate.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task GetByProjectAsync_AsProjectMember_IsAllowed()
    {
        var memberId = Guid.NewGuid();
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = _projectA,
            UserId = memberId,
            Role = "Developer"
        });
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = _orgA,
            UserId = memberId,
            Role = OrganizationRoleRules.Member
        });
        await _context.SaveChangesAsync();
        ActAs(memberId);

        var result = await _service.GetByProjectAsync(_projectA);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_AsRegularProjectMember_IsForbidden()
    {
        var memberId = Guid.NewGuid();
        _context.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = _projectA,
            UserId = memberId,
            Role = "Developer"
        });
        _context.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = _orgA,
            UserId = memberId,
            Role = OrganizationRoleRules.Member
        });
        await _context.SaveChangesAsync();
        ActAs(memberId);

        var result = await _service.CreateAsync(_projectA, NewRepoDto(_installationA));

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateAsync_WithInactiveInstallation_IsRejected()
    {
        ActAs(_userA);
        var installation = await _context.GitHubInstallations.FindAsync(_installationA);
        installation!.Status = "Suspended";
        await _context.SaveChangesAsync();

        var result = await _service.CreateAsync(_projectA, NewRepoDto(_installationA));

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetByProjectAsync_WithoutAuthenticatedUser_IsForbidden()
    {
        _currentUser.Setup(x => x.UserId).Returns((Guid?)null);

        var result = await _service.GetByProjectAsync(_projectA);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task RemoveThenCreate_SameRepository_ReactivatesWithoutDeletingHistory()
    {
        ActAs(_userA);
        var created = await _service.CreateAsync(_projectA, NewRepoDto(_installationA));

        (await _service.RemoveAsync(_projectA, created.Data!.Id)).IsSuccess.Should().BeTrue();
        (await _service.GetByProjectAsync(_projectA)).Data.Should().ContainSingle(x => !x.IsActive);

        var reactivated = await _service.CreateAsync(_projectA, NewRepoDto(_installationA));

        reactivated.IsSuccess.Should().BeTrue();
        reactivated.Data!.Id.Should().Be(created.Data.Id);
        reactivated.Data.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithInvalidRepositoryMetadata_IsRejected()
    {
        ActAs(_userA);
        var invalid = NewRepoDto(_installationA) with { RepositoryExternalId = 0, Owner = " " };

        var result = await _service.CreateAsync(_projectA, invalid);

        result.StatusCode.Should().Be(400);
    }
}
