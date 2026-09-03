using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.DTOs.Project;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

#pragma warning disable CA1707
public sealed class OrganizationServiceTests : IDisposable
{
    private readonly QalyDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IAuditLogService> _audit = new();

    public OrganizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new QalyDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_CommitsOrganizationOwnerAndProfessionalProfilesAsOneGraph()
    {
        var ownerId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = ownerId,
            FullName = "Organization Owner",
            Email = "organization-owner@qaly.dev",
            IsActive = true
        });
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(service => service.UserId).Returns(ownerId);
        _currentUser.SetupGet(service => service.Role).Returns("Member");
        var unitOfWork = new CountingUnitOfWork(_context);
        var service = CreateService(unitOfWork);

        var result = await service.CreateAsync(new CreateOrganizationDto(
            "Atomic Organization",
            "atomic-organization",
            "Organization graph regression",
            null));

        result.IsSuccess.Should().BeTrue(result.Error);
        var organization = await _context.Organizations.SingleAsync();
        var owner = await _context.OrganizationMembers.SingleAsync();
        owner.OrganizationId.Should().Be(organization.Id);
        owner.UserId.Should().Be(ownerId);
        owner.Role.Should().Be(OrganizationRoleRules.Owner);
        (await _context.ProfessionalProfileDefinitions.CountAsync(profile =>
            profile.OrganizationId == organization.Id)).Should().BeGreaterThan(0);
        unitOfWork.SaveCount.Should().Be(1,
            "the Organization, mandatory owner membership and baseline professional profiles must commit atomically");
    }

    [Fact]
    public async Task CreateAsync_WhenAuditStagingFails_DoesNotCommitCanonicalGraph()
    {
        var ownerId = Guid.NewGuid();
        _context.Users.Add(new User
        {
            Id = ownerId,
            FullName = "Organization Owner",
            Email = "organization-audit-failure@qaly.dev",
            IsActive = true
        });
        await _context.SaveChangesAsync();

        _currentUser.SetupGet(service => service.UserId).Returns(ownerId);
        _currentUser.SetupGet(service => service.Role).Returns("Member");
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

        var action = () => service.CreateAsync(new CreateOrganizationDto(
            "Must Roll Back",
            "must-roll-back",
            null,
            null));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("simulated audit staging failure");
        unitOfWork.SaveCount.Should().Be(0,
            "canonical data must not commit after audit staging failed");

        _context.ChangeTracker.Clear();
        (await _context.Organizations.CountAsync()).Should().Be(0);
        (await _context.OrganizationMembers.CountAsync()).Should().Be(0);
        (await _context.ProfessionalProfileDefinitions.CountAsync()).Should().Be(0);
    }

    private OrganizationService CreateService(IUnitOfWork unitOfWork) => new(
        new GenericRepository<Organization>(_context),
        new GenericRepository<OrganizationMember>(_context),
        new GenericRepository<User>(_context),
        new GenericRepository<ModeratorAssignment>(_context),
        new GenericRepository<ProfessionalProfileDefinition>(_context),
        unitOfWork,
        _currentUser.Object,
        _audit.Object);

    public void Dispose() => _context.Dispose();

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
