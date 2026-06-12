using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class AiGeneratedDraftConfiguration : IEntityTypeConfiguration<AiGeneratedDraft>
{
    public void Configure(EntityTypeBuilder<AiGeneratedDraft> builder)
    {
        builder.HasKey(draft => draft.Id);
        builder.Property(draft => draft.Id).HasDefaultValueSql("NEWID()");

        builder.Property(draft => draft.DraftType).HasMaxLength(80).IsRequired();
        builder.Property(draft => draft.PayloadJson).IsRequired();
        builder.Property(draft => draft.Status).HasMaxLength(40).IsRequired();
        builder.Property(draft => draft.ConfirmAction).HasMaxLength(80);
        builder.Property(draft => draft.ConfirmationNote).HasMaxLength(1000);
        builder.Property(draft => draft.SchemaId).HasMaxLength(100);
        builder.Property(draft => draft.Confidence).HasColumnType("decimal(5,4)");
        builder.Property(draft => draft.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(draft => draft.AiJob)
            .WithMany(job => job.Drafts)
            .HasForeignKey(draft => draft.AiJobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(draft => draft.Project)
            .WithMany(project => project.AiDrafts)
            .HasForeignKey(draft => draft.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(draft => draft.ConfirmedBy)
            .WithMany(user => user.ConfirmedAiDrafts)
            .HasForeignKey(draft => draft.ConfirmedById)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(draft => new { draft.ProjectId, draft.Status });
        builder.HasIndex(draft => draft.AiJobId);
    }
}
