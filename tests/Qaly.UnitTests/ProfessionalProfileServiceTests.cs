using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.DTOs.Organization;
using Qaly.Application.Services;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class ProfessionalProfileServiceTests : IDisposable
{
    private readonly QalyDbContext _db;
    private readonly Mock<ICurrentUserService> _current = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();
    private readonly ProfessionalProfileDefinition _managerProfile;
    private readonly ProfessionalProfileDefinition _developerProfile;

    public ProfessionalProfileServiceTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _db.Users.AddRange(
            new User { Id = _ownerId, FullName = "Owner", Email = "owner@qaly.dev", Role = "Member", IsActive = true },
            new User { Id = _memberId, FullName = "Member", Email = "member@qaly.dev", Role = "Member", IsActive = true });
        _db.Organizations.Add(new Organization { Id = _organizationId, OwnerId = _ownerId, Name = "Qaly", Code = "qaly", IsActive = true });
        _db.OrganizationMembers.AddRange(
            new OrganizationMember { OrganizationId = _organizationId, UserId = _ownerId, Role = OrganizationRoleRules.Owner },
            new OrganizationMember { OrganizationId = _organizationId, UserId = _memberId, Role = OrganizationRoleRules.Member });
        _managerProfile = new ProfessionalProfileDefinition { OrganizationId = _organizationId, Key = "product-project-manager", Name = "Product / Project Manager", Category = "Delivery", IsActive = true };
        _developerProfile = new ProfessionalProfileDefinition { OrganizationId = _organizationId, Key = "backend-engineer", Name = "Backend Engineer", Category = "Engineering", IsActive = true };
        _db.ProfessionalProfileDefinitions.AddRange(_managerProfile, _developerProfile);
        _db.SaveChanges();
        SetActor(_ownerId);
    }

    [Fact]
    public async Task ManagerCanVerifyProfile_WithoutChangingAccessRole_AndGetsCanonicalReadback()
    {
        var result = await Service().ReplaceMemberProfilesAsync(_organizationId, _memberId,
            Request(new(_developerProfile.Id, "Proficient", "Verified", "ManagerConfirmed", null, null, "Reviewed portfolio")));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Profiles.Should().ContainSingle(item => item.Key == "backend-engineer" && item.VerificationStatus == "Verified");
        result.Data.AuthorizationNotice.Should().Contain("không cấp quyền truy cập");
        (await _db.OrganizationMembers.SingleAsync(item => item.UserId == _memberId)).Role.Should().Be(OrganizationRoleRules.Member);
        var stored = await _db.OrganizationMemberProfessionalProfiles.SingleAsync();
        stored.VerifiedByUserId.Should().Be(_ownerId);
        stored.VerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MemberCanDeclareOwnProfile_ButCannotSelfVerify()
    {
        SetActor(_memberId);
        var declared = await Service().ReplaceMemberProfilesAsync(_organizationId, _memberId,
            Request(new(_developerProfile.Id, "Practitioner", "Declared", "MemberDeclared", null, null, null)));
        declared.IsSuccess.Should().BeTrue(declared.Error);

        var row = declared.Data!.Profiles.Single();
        var verify = await Service().ReplaceMemberProfilesAsync(_organizationId, _memberId,
            new ReplaceMemberProfessionalProfilesDto(true,
                [new(_developerProfile.Id, "Expert", "Verified", "ManagerConfirmed", null, null, null)],
                [new(row.AssignmentId, row.RowVersion)]));

        verify.StatusCode.Should().Be(403);
        (await _db.OrganizationMemberProfessionalProfiles.SingleAsync()).VerificationStatus.Should().Be("Declared");
    }

    [Fact]
    public async Task MemberCannotChangeAnotherMembersProfiles()
    {
        var otherId = Guid.NewGuid();
        _db.Users.Add(new User { Id = otherId, FullName = "Other", Email = "other@qaly.dev", IsActive = true });
        _db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = _organizationId, UserId = otherId, Role = OrganizationRoleRules.Member });
        await _db.SaveChangesAsync();
        SetActor(_memberId);

        var result = await Service().ReplaceMemberProfilesAsync(_organizationId, otherId,
            Request(new(_developerProfile.Id, "Practitioner", "Declared", null, null, null, null)));

        result.StatusCode.Should().Be(404, "the endpoint must not disclose a writable target across the authorization boundary");
        (await _db.OrganizationMemberProfessionalProfiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CrossOrganizationDefinitionIsRejected()
    {
        var foreign = new ProfessionalProfileDefinition { OrganizationId = Guid.NewGuid(), Key = "foreign", Name = "Foreign", Category = "Other", IsActive = true };
        _db.ProfessionalProfileDefinitions.Add(foreign);
        await _db.SaveChangesAsync();

        var result = await Service().ReplaceMemberProfilesAsync(_organizationId, _memberId,
            Request(new(foreign.Id, "Practitioner", "Verified", null, null, null, null)));

        result.StatusCode.Should().Be(422);
        (await _db.OrganizationMemberProfessionalProfiles.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReplaceRejectsStaleKnownRows()
    {
        var entity = new OrganizationMemberProfessionalProfile
        {
            OrganizationId = _organizationId,
            UserId = _memberId,
            ProfessionalProfileDefinitionId = _developerProfile.Id,
            Proficiency = "Practitioner",
            VerificationStatus = "Declared",
            Source = "MemberDeclared",
            RowVersion = [1, 2, 3]
        };
        _db.OrganizationMemberProfessionalProfiles.Add(entity);
        await _db.SaveChangesAsync();

        var result = await Service().ReplaceMemberProfilesAsync(_organizationId, _memberId,
            new ReplaceMemberProfessionalProfilesDto(true,
                [new(_managerProfile.Id, "Expert", "Verified", null, null, null, null)],
                [new(entity.Id, Convert.ToBase64String([9, 9, 9]))]));

        result.StatusCode.Should().Be(409);
        (await _db.OrganizationMemberProfessionalProfiles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ModeratorViewScopeCanReadButCannotManageProfessionalProfiles()
    {
        var moderatorId = await AddModeratorAsync(ModeratorCapabilities.ProfessionalProfilesView);
        SetActor(moderatorId, SystemRoleRules.Moderator);

        var read = await Service().GetMemberProfilesAsync(_organizationId, _memberId);
        var write = await Service().ReplaceMemberProfilesAsync(_organizationId, _memberId,
            Request(new(_developerProfile.Id, "Practitioner", "Verified", null, null, null, null)));

        read.IsSuccess.Should().BeTrue(read.Error);
        write.StatusCode.Should().Be(404, "view capability must never imply profile mutation");
    }

    [Fact]
    public async Task ModeratorManageScopeCanVerifyAndGetsCanonicalReadback()
    {
        var moderatorId = await AddModeratorAsync(ModeratorCapabilities.ProfessionalProfilesManage);
        SetActor(moderatorId, SystemRoleRules.Moderator);

        var result = await Service().ReplaceMemberProfilesAsync(_organizationId, _memberId,
            Request(new(_developerProfile.Id, "Proficient", "Verified", null, null, null, "Delegated review")));

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.Profiles.Should().ContainSingle(item => item.VerificationStatus == "Verified");
        result.Data.Profiles.Single().VerifiedByUserId.Should().Be(moderatorId);
    }

    private static ReplaceMemberProfessionalProfilesDto Request(MemberProfessionalProfileSelectionDto profile)
        => new(true, [profile], []);

    private ProfessionalProfileService Service()
        => new(
            new GenericRepository<Organization>(_db),
            new GenericRepository<OrganizationMember>(_db),
            new GenericRepository<User>(_db),
            new GenericRepository<ProfessionalProfileDefinition>(_db),
            new GenericRepository<OrganizationMemberProfessionalProfile>(_db),
            new GenericRepository<ModeratorAssignment>(_db),
            _current.Object,
            new UnitOfWork(_db),
            _audit.Object);

    private async Task<Guid> AddModeratorAsync(string capability)
    {
        var moderatorId = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = moderatorId,
            FullName = "Scoped Moderator",
            Email = $"moderator-{Guid.NewGuid():N}@qaly.dev",
            Role = SystemRoleRules.Moderator,
            IsActive = true
        });
        _db.ModeratorAssignments.Add(new ModeratorAssignment
        {
            ModeratorUserId = moderatorId,
            OrganizationId = _organizationId,
            GrantedByUserId = _ownerId,
            Capability = capability,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
        await _db.SaveChangesAsync();
        return moderatorId;
    }

    private void SetActor(Guid userId, string role = "Member")
    {
        _current.SetupGet(item => item.UserId).Returns(userId);
        _current.SetupGet(item => item.Role).Returns(role);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}
