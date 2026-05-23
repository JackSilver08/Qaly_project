using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasDefaultValueSql("NEWID()");

        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Planning");
        builder.Property(s => s.Goal).HasMaxLength(1000);
        builder.Property(s => s.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(s => s.Project)
            .WithMany(p => p.Sprints)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes (Phase 2 requirement)
        builder.HasIndex(s => s.ProjectId);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => new { s.ProjectId, s.Status });
    }
}
