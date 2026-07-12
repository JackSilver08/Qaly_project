using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("NEWID()");

        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Code).HasMaxLength(80).IsRequired();
        builder.Property(p => p.LogoUrl).HasMaxLength(1000);
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        // Plain column (not store-generated) so in-memory increments during
        // new-project + task creation are persisted correctly.
        builder.Property(p => p.TaskSequence);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(p => p.Owner)
            .WithMany(u => u.OwnedProjects)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Organization)
            .WithMany(o => o.Projects)
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(p => p.SourceGroup)
            .WithMany(group => group.CreatedProjects)
            .HasForeignKey(p => p.SourceGroupId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(p => p.Code).IsUnique();
        builder.HasIndex(p => p.OrganizationId);
        builder.HasIndex(p => p.SourceGroupId);
    }
}
