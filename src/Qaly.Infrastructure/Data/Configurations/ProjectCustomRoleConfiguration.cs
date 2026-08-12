using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class ProjectCustomRoleConfiguration : IEntityTypeConfiguration<ProjectCustomRole>
{
    public void Configure(EntityTypeBuilder<ProjectCustomRole> builder)
    {
        builder.ToTable("ProjectCustomRoles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Description)
            .HasMaxLength(500);

        builder.Property(r => r.ColorCode)
            .HasMaxLength(20);

        builder.HasOne(r => r.Project)
            .WithMany()
            .HasForeignKey(r => r.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.ProjectId, r.Name });
    }
}
