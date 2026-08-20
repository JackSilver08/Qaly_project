using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class ProjectMemberRoleHistoryConfiguration : IEntityTypeConfiguration<ProjectMemberRoleHistory>
{
    public void Configure(EntityTypeBuilder<ProjectMemberRoleHistory> builder)
    {
        builder.ToTable("ProjectMemberRoleHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.PhaseName)
            .HasMaxLength(150);

        builder.Property(h => h.ReasonOrNote)
            .HasMaxLength(500);

        builder.HasOne(h => h.ProjectMember)
            .WithMany()
            .HasForeignKey(h => h.ProjectMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Role)
            .WithMany(r => r.RoleHistories)
            .HasForeignKey(h => h.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.AssignedByUser)
            .WithMany()
            .HasForeignKey(h => h.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique Filtered Index: Đảm bảo ở cấp Database chỉ duy nhất 1 Active Role per Member (EndDate IS NULL)
        builder.HasIndex(h => h.ProjectMemberId)
            .IsUnique()
            .HasFilter("[EndDate] IS NULL");
    }
}
