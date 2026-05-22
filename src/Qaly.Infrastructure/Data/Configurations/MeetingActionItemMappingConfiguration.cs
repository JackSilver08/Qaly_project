using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class MeetingActionItemMappingConfiguration : IEntityTypeConfiguration<MeetingActionItemMapping>
{
    public void Configure(EntityTypeBuilder<MeetingActionItemMapping> builder)
    {
        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id).HasDefaultValueSql("NEWID()");

        builder.Property(mapping => mapping.Status)
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(mapping => mapping.SourceTitle)
            .HasMaxLength(300);

        builder.Property(mapping => mapping.SourcePriority)
            .HasMaxLength(20);

        builder.Property(mapping => mapping.SourceQuote)
            .HasMaxLength(2000);

        builder.Property(mapping => mapping.CreatedAt)
            .HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(mapping => mapping.MeetingImport)
            .WithMany(import => import.ActionItemMappings)
            .HasForeignKey(mapping => mapping.MeetingImportId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mapping => mapping.Task)
            .WithMany()
            .HasForeignKey(mapping => mapping.TaskId)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        builder.HasOne(mapping => mapping.CreatedBy)
            .WithMany()
            .HasForeignKey(mapping => mapping.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(mapping => new { mapping.MeetingImportId, mapping.ActionItemIndex }).IsUnique();
        builder.HasIndex(mapping => mapping.TaskId);
        builder.HasIndex(mapping => mapping.CreatedById);
    }
}
