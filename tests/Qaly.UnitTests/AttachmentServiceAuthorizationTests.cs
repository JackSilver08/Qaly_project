using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Application.Services;
using Qaly.Application.Services.Tasks;
using Qaly.Domain.Entities;
using Qaly.Domain.Interfaces;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Data.Repositories;

namespace Qaly.UnitTests;

public sealed class AttachmentServiceAuthorizationTests : IDisposable
{
    private readonly QalyDbContext _db;

    public AttachmentServiceAuthorizationTests()
    {
        _db = new QalyDbContext(new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    [Fact]
    public async Task ReviewEvidence_CustomReviewerRole_UsesInheritedCapability()
    {
        var fixture = await SeedAsync("quality-reviewer", customBaseRole: ProjectRoleRules.Reviewer);

        var result = await CreateService(fixture.ActorId).ReviewEvidenceAsync(
            fixture.AttachmentId, true, "Verified against acceptance criteria");

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.EvidenceApprovalStatus.Should().Be("Approved");
        result.Data.EvidenceReviewedById.Should().Be(fixture.ActorId);
    }

    [Fact]
    public async Task ReviewEvidence_ViewerRole_IsDenied()
    {
        var fixture = await SeedAsync(ProjectRoleRules.Viewer);

        var result = await CreateService(fixture.ActorId).ReviewEvidenceAsync(
            fixture.AttachmentId, true, null);

        result.StatusCode.Should().Be(403);
        (await _db.TaskAttachments.FindAsync(fixture.AttachmentId))!
            .EvidenceApprovalStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task Upload_ViewerRole_IsDeniedBeforeFileStorageMutation()
    {
        var fixture = await SeedAsync(ProjectRoleRules.Viewer);
        await using var content = new MemoryStream([1, 2, 3]);

        var result = await CreateService(fixture.ActorId).UploadAsync(
            (await _db.TaskAttachments.SingleAsync(item => item.Id == fixture.AttachmentId)).TaskItemId!.Value,
            "forbidden.txt",
            "text/plain",
            content.Length,
            content);

        result.StatusCode.Should().Be(403);
        _db.TaskAttachments.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_ViewerRole_IsDeniedForAnotherUsersAttachment()
    {
        var fixture = await SeedAsync(ProjectRoleRules.Viewer);

        var result = await CreateService(fixture.ActorId).DeleteAsync(fixture.AttachmentId);

        result.StatusCode.Should().Be(403);
        (await _db.TaskAttachments.FindAsync(fixture.AttachmentId)).Should().NotBeNull();
    }

    [Fact]
    public async Task ReviewEvidence_OrganizationAdmin_CanReviewPublicButNotUnrelatedPrivateTask()
    {
        var publicFixture = await SeedAsync(
            projectRole: null,
            organizationRole: OrganizationRoleRules.OrganizationAdmin,
            isPrivate: false);
        var privateFixture = await SeedAsync(
            projectRole: null,
            organizationRole: OrganizationRoleRules.OrganizationAdmin,
            isPrivate: true);

        var service = CreateService(publicFixture.ActorId);
        var publicResult = await service.ReviewEvidenceAsync(publicFixture.AttachmentId, true, null);
        var privateResult = await service.ReviewEvidenceAsync(privateFixture.AttachmentId, true, null);

        publicResult.IsSuccess.Should().BeTrue(publicResult.Error);
        privateResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Upload_MismatchedSize_IsRejectedBeforeStorageOrMetadataMutation()
    {
        var fixture = await SeedAsync(ProjectRoleRules.Member);
        await using var content = new MemoryStream([1, 2, 3, 4]);

        var result = await CreateService(fixture.ActorId).UploadAsync(
            (await _db.TaskAttachments.SingleAsync(item => item.Id == fixture.AttachmentId)).TaskItemId!.Value,
            "mismatch.txt",
            "text/plain",
            99,
            content);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        _db.TaskAttachments.Should().ContainSingle();
        _db.PhysicalFiles.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_ProjectManager_DecrementsCanonicalReferenceCount()
    {
        var fixture = await SeedAsync(ProjectRoleRules.Manager);
        var physicalFileId = (await _db.TaskAttachments.SingleAsync(item => item.Id == fixture.AttachmentId)).PhysicalFileId;

        var result = await CreateService(fixture.ActorId).DeleteAsync(fixture.AttachmentId);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await _db.PhysicalFiles.AsNoTracking().SingleAsync(file => file.Id == physicalFileId))
            .ReferenceCount.Should().Be(0);
        (await _db.TaskAttachments.IgnoreQueryFilters().SingleAsync(item => item.Id == fixture.AttachmentId))
            .IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task StorageMaintenance_ServiceBoundaryRequiresSystemAdmin()
    {
        var fixture = await SeedAsync(ProjectRoleRules.Manager);
        var service = CreateService(fixture.ActorId);

        (await service.GetDuplicatesAsync()).StatusCode.Should().Be(403);
        (await service.DeduplicateAsync()).StatusCode.Should().Be(403);
        (await service.GetStorageStatsAsync()).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Deduplicate_CommitsCanonicalReferencesBeforeStorageCleanup_AndReportsCleanupFailure()
    {
        var first = await SeedAsync(ProjectRoleRules.Member);
        await SeedAsync(ProjectRoleRules.Member);
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream([1, 2, 3, 4]));
        storage.Setup(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage unavailable"));

        var result = await CreateService(first.ActorId, SystemRoleRules.Admin, storage.Object)
            .DeduplicateAsync();

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Data!.TotalMerged.Should().Be(1);
        result.Data.CleanupFailures.Should().Be(1);
        result.Data.BytesSaved.Should().Be(0);
        result.Data.Warnings.Should().ContainSingle();
        _db.PhysicalFiles.Should().ContainSingle();
        var canonicalIds = await _db.TaskAttachments.IgnoreQueryFilters()
            .Select(attachment => attachment.PhysicalFileId)
            .Distinct()
            .ToListAsync();
        canonicalIds.Should().ContainSingle();
    }

    [Fact]
    public async Task GetDuplicates_WhenAnyPhysicalFileCannotBeRead_DoesNotReturnPartialSuccess()
    {
        var fixture = await SeedAsync(ProjectRoleRules.Member);
        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.DownloadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("missing blob"));

        var result = await CreateService(fixture.ActorId, SystemRoleRules.Admin, storage.Object)
            .GetDuplicatesAsync();

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(503);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<(Guid ActorId, Guid AttachmentId)> SeedAsync(
        string? projectRole,
        string? customBaseRole = null,
        string organizationRole = OrganizationRoleRules.Member,
        bool isPrivate = false)
    {
        var ownerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var physicalFileId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var owner = User(ownerId, $"owner-{ownerId:N}@qaly.test", "Owner");
        var actor = User(actorId, $"actor-{actorId:N}@qaly.test", "Reviewer");
        _db.Users.AddRange(owner, actor);
        _db.Organizations.Add(new Organization
        {
            Id = organizationId,
            Name = $"Organization {organizationId:N}",
            Code = $"O{organizationId:N}"[..12],
            OwnerId = ownerId,
            IsActive = true
        });
        _db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = actorId,
            Role = organizationRole
        });
        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "Evidence project",
            Code = $"P{projectId:N}"[..12],
            OwnerId = ownerId,
            OrganizationId = organizationId
        });
        if (!string.IsNullOrWhiteSpace(projectRole))
        {
            _db.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = projectId,
                UserId = actorId,
                Role = projectRole
            });
        }
        if (!string.IsNullOrWhiteSpace(customBaseRole))
        {
            _db.ProjectRoleDefinitions.Add(new ProjectRoleDefinition
            {
                OrganizationId = organizationId,
                Key = projectRole!,
                DisplayName = "Quality reviewer",
                BaseRole = customBaseRole,
                CreatedByUserId = ownerId,
                IsActive = true
            });
        }
        _db.TaskItems.Add(new TaskItem
        {
            Id = taskId,
            ProjectId = projectId,
            ReporterId = ownerId,
            Title = "Private evidence",
            IsPrivate = isPrivate
        });
        _db.PhysicalFiles.Add(new PhysicalFile
        {
            Id = physicalFileId,
            ContentHash = Guid.NewGuid().ToString("N"),
            FilePath = "/evidence.txt",
            FileSize = 42
        });
        _db.TaskAttachments.Add(new TaskAttachment
        {
            Id = attachmentId,
            TaskItemId = taskId,
            UploadedById = ownerId,
            PhysicalFileId = physicalFileId,
            FileName = "evidence.txt",
            IsEvidence = true,
            EvidenceApprovalStatus = "Pending"
        });
        await _db.SaveChangesAsync();
        return (actorId, attachmentId);
    }

    private AttachmentService CreateService(
        Guid actorId,
        string systemRole = "User",
        IFileStorageService? storage = null,
        INotificationService? notifications = null)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns(actorId);
        currentUser.SetupGet(service => service.Role).Returns(systemRole);
        currentUser.SetupGet(service => service.IsAuthenticated).Returns(true);
        var projectRepo = new GenericRepository<Project>(_db);
        var projectMemberRepo = new GenericRepository<ProjectMember>(_db);
        var roleCatalog = new ProjectRoleCatalog(new GenericRepository<ProjectRoleDefinition>(_db));
        var policy = new TaskAccessPolicy(
            currentUser.Object,
            projectRepo,
            projectMemberRepo,
            new GenericRepository<OrganizationMember>(_db),
            roleCatalog);

        return new AttachmentService(
            new GenericRepository<TaskAttachment>(_db),
            new GenericRepository<PhysicalFile>(_db),
            new GenericRepository<TaskItem>(_db),
            projectMemberRepo,
            policy,
            roleCatalog,
            storage ?? Mock.Of<IFileStorageService>(),
            new UnitOfWork(_db),
            currentUser.Object,
            Mock.Of<IAuditLogService>(),
            notifications ?? Mock.Of<INotificationService>());
    }

    private static User User(Guid id, string email, string name)
        => new() { Id = id, Email = email, FullName = name, PasswordHash = "test", IsActive = true };
}
