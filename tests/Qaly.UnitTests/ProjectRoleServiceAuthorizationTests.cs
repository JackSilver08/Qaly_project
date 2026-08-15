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

public sealed class ProjectRoleServiceAuthorizationTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAuditLogService> _audit = new();

    public ProjectRoleServiceAuthorizationTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    [Fact]
    public async Task PlainProjectMember_CannotCreateOrAssignLegacyCustomRole()
    {
        var ownerId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        _db.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = "legacy-owner@qaly.dev", IsActive = true },
            new User { Id = memberUserId, FullName = "Member", Email = "legacy-member@qaly.dev", IsActive = true });
        _db.Projects.Add(new Project { Id = projectId, OwnerId = ownerId, Name = "Legacy RBAC", Code = "legacy-rbac" });
        _db.ProjectMembers.Add(new ProjectMember
        {
            Id = memberId,
            ProjectId = projectId,
            UserId = memberUserId,
            Role = ProjectRoleRules.Member
        });
        _db.ProjectCustomRoles.Add(new ProjectCustomRole
        {
            Id = roleId,
            ProjectId = projectId,
            Name = "Legacy role"
        });
        await _db.SaveChangesAsync();
        _currentUser.SetupGet(service => service.UserId).Returns(memberUserId);

        var service = CreateService();
        var create = await service.CreateCustomRoleAsync(projectId,
            new CreateProjectCustomRoleDto("Spoof", null, null, "{}"));
        var assign = await service.AssignRoleAsync(projectId, memberId,
            new AssignProjectMemberRoleDto(roleId, null, DateTimeOffset.UtcNow, null));

        create.StatusCode.Should().Be(403);
        assign.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Outsider_CannotListLegacyCustomRoles()
    {
        var ownerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _db.Users.AddRange(
            new User { Id = ownerId, FullName = "Owner", Email = "owner2@qaly.dev", IsActive = true },
            new User { Id = outsiderId, FullName = "Outsider", Email = "outsider@qaly.dev", IsActive = true });
        _db.Projects.Add(new Project { Id = projectId, OwnerId = ownerId, Name = "Secret", Code = "secret-role" });
        await _db.SaveChangesAsync();
        _currentUser.SetupGet(service => service.UserId).Returns(outsiderId);

        var result = await CreateService().GetCustomRolesAsync(projectId);

        result.StatusCode.Should().Be(403);
    }

    private ProjectRoleService CreateService()
        => new(
            new GenericRepository<ProjectCustomRole>(_db),
            new GenericRepository<ProjectMemberRoleHistory>(_db),
            new GenericRepository<ProjectMember>(_db),
            new GenericRepository<Project>(_db),
            new GenericRepository<SystemModulePermission>(_db),
            new GenericRepository<User>(_db),
            new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_db)),
            new UnitOfWork(_db),
            _currentUser.Object,
            _audit.Object);

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
