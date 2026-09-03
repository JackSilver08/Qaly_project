using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class SystemModulePermissionConfiguration : IEntityTypeConfiguration<SystemModulePermission>
{
    public void Configure(EntityTypeBuilder<SystemModulePermission> builder)
    {
        builder.ToTable("SystemModulePermissions", table => table.HasCheckConstraint(
            "CK_SystemModulePermissions_ExactlyOneScope",
            "([UserId] IS NOT NULL AND [SystemRole] IS NULL) OR ([UserId] IS NULL AND [SystemRole] IS NOT NULL)"));

        builder.HasKey(p => p.Id);

        builder.Property(p => p.ModuleKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.SystemRole)
            .HasMaxLength(50);

        builder.Property(p => p.AiTier)
            .HasMaxLength(50)
            .HasDefaultValue("Full");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.SystemRole, p.ModuleKey })
            .IsUnique()
            .HasFilter("[SystemRole] IS NOT NULL AND [UserId] IS NULL");
        builder.HasIndex(p => new { p.UserId, p.ModuleKey })
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL AND [SystemRole] IS NULL");
    }
}
