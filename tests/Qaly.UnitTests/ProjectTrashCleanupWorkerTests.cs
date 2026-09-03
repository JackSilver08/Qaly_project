using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Qaly.Application.Common.Interfaces;
using Qaly.Domain.Entities;
using Qaly.Infrastructure.Data;
using Qaly.Infrastructure.Services;

namespace Qaly.UnitTests;

public sealed class ProjectTrashCleanupWorkerTests
{
    [Fact]
    public async Task AttachmentStorageFailure_PreservesCanonicalMetadataForRetry()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<QalyDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .Options;
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var physicalFileId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();

        await using (var seed = new QalyDbContext(options))
        {
            var owner = new User
            {
                Id = ownerId,
                FullName = "Trash recovery owner",
                Email = "trash-recovery@qaly.test",
                PasswordHash = "test"
            };
            var project = new Project
            {
                Id = projectId,
                Name = "Expired trash Project",
                Code = "TRASH-RECOVERY",
                OwnerId = ownerId,
                Owner = owner,
                IsDeleted = true,
                DeletedAt = DateTimeOffset.UtcNow.AddDays(-31)
            };
            var physicalFile = new PhysicalFile
            {
                Id = physicalFileId,
                ContentHash = Guid.NewGuid().ToString("N"),
                FilePath = "unavailable/project-file.bin",
                FileSize = 128,
                ReferenceCount = 1
            };
            seed.TaskAttachments.Add(new TaskAttachment
            {
                Id = attachmentId,
                ProjectId = projectId,
                Project = project,
                PhysicalFileId = physicalFileId,
                PhysicalFile = physicalFile,
                UploadedById = ownerId,
                UploadedBy = owner,
                FileName = "project-file.bin",
                Scope = "Project"
            });
            await seed.SaveChangesAsync();
        }

        var storage = new Mock<IFileStorageService>();
        storage.Setup(service => service.DeleteAsync(
                "unavailable/project-file.bin",
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Storage is temporarily unavailable."));
        var worker = new ProjectTrashCleanupWorker(
            Mock.Of<IServiceProvider>(),
            NullLogger<ProjectTrashCleanupWorker>.Instance);

        await using (var attempt = new QalyDbContext(options))
        {
            var act = () => worker.HardDeleteProjectAttachmentsAsync(
                attempt,
                storage.Object,
                projectId,
                CancellationToken.None);

            await act.Should().ThrowAsync<IOException>();
        }

        await using var readBack = new QalyDbContext(options);
        (await readBack.Projects.IgnoreQueryFilters().SingleAsync(item => item.Id == projectId))
            .IsDeleted.Should().BeTrue();
        (await readBack.TaskAttachments.IgnoreQueryFilters().SingleAsync(item => item.Id == attachmentId))
            .PhysicalFileId.Should().Be(physicalFileId);
        (await readBack.PhysicalFiles.SingleAsync(item => item.Id == physicalFileId))
            .ReferenceCount.Should().Be(1);
    }
}
