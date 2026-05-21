using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class MeetingImportConfiguration : IEntityTypeConfiguration<MeetingImport>
{
    public void Configure(EntityTypeBuilder<MeetingImport> builder)
    {
        builder.HasKey(import => import.Id);
        builder.Property(import => import.Id).HasDefaultValueSql("NEWID()");

        builder.Property(import => import.SourceProvider).HasMaxLength(40).IsRequired();
        builder.Property(import => import.SourceId).HasMaxLength(200).IsRequired();
        builder.Property(import => import.SourceHash).HasMaxLength(64).IsRequired();
        builder.Property(import => import.Title).HasMaxLength(300).IsRequired();
        builder.Property(import => import.Summary).HasMaxLength(4000);
        builder.Property(import => import.TranscriptText).IsRequired();
        builder.Property(import => import.ParticipantsJson).IsRequired();
        builder.Property(import => import.RawPayloadJson).IsRequired();
        builder.Property(import => import.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(import => import.Project)
            .WithMany()
            .HasForeignKey(import => import.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(import => import.ImportedBy)
            .WithMany()
            .HasForeignKey(import => import.ImportedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(import => import.AiJob)
            .WithMany()
            .HasForeignKey(import => import.AiJobId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        builder.HasOne(import => import.AiDraft)
            .WithMany()
            .HasForeignKey(import => import.AiDraftId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        builder.HasIndex(import => new { import.ProjectId, import.SourceProvider, import.SourceHash }).IsUnique();
        builder.HasIndex(import => import.AiJobId);
        builder.HasIndex(import => import.AiDraftId);
    }
}
