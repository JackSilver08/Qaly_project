using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class TaskAttachmentConfiguration : IEntityTypeConfiguration<TaskAttachment>
{
    public void Configure(EntityTypeBuilder<TaskAttachment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasDefaultValueSql("NEWID()");
        builder.Property(a => a.FileName).HasMaxLength(500).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100);
        builder.Property(a => a.Scope).HasMaxLength(20).IsRequired().HasDefaultValue("Task");
        builder.Property(a => a.IsEvidence).HasDefaultValue(false);
        builder.Property(a => a.EvidenceApprovalStatus).HasMaxLength(20).IsRequired().HasDefaultValue("None");
        builder.Property(a => a.EvidenceReviewNote).HasMaxLength(1000);
        builder.Property(a => a.UploadedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(a => a.PhysicalFile)
            .WithMany()
            .HasForeignKey(a => a.PhysicalFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(a => a.FilePath);
        builder.Ignore(a => a.FileSize);
        builder.Ignore(a => a.ContentHash);

        builder.HasOne(a => a.TaskItem)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(a => a.Project)
            .WithMany(p => p.Attachments)
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(a => a.Comment)
            .WithMany(c => c.Attachments)
            .HasForeignKey(a => a.CommentId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.EvidenceReviewedBy)
            .WithMany()
            .HasForeignKey(a => a.EvidenceReviewedById)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(a => new { a.Scope, a.ProjectId });
        builder.HasIndex(a => new { a.Scope, a.TaskItemId });
        builder.HasIndex(a => new { a.Scope, a.CommentId });
        builder.HasIndex(a => new { a.TaskItemId, a.IsEvidence, a.EvidenceApprovalStatus });
    }
}
