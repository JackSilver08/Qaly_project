using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasKey(pm => pm.Id);
        builder.Property(pm => pm.Id).HasDefaultValueSql("NEWID()");
        builder.Property(pm => pm.Role).HasMaxLength(20).IsRequired();
        builder.Property(pm => pm.JoinedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(pm => pm.Project)
            .WithMany(p => p.Members)
            .HasForeignKey(pm => pm.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pm => pm.User)
            .WithMany(u => u.ProjectMemberships)
            .HasForeignKey(pm => pm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique: 1 user chỉ có 1 membership trong 1 project
        builder.HasIndex(pm => new { pm.ProjectId, pm.UserId }).IsUnique();
        builder.HasIndex(pm => pm.UserId);
    }
}
