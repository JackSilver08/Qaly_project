using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ModeratorAssignmentConfiguration : IEntityTypeConfiguration<ModeratorAssignment>
{
    public void Configure(EntityTypeBuilder<ModeratorAssignment> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Capability).HasMaxLength(100).IsRequired();
        builder.Property(item => item.IsActive).HasDefaultValue(true);

        builder.HasOne(item => item.ModeratorUser).WithMany()
            .HasForeignKey(item => item.ModeratorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.GrantedByUser).WithMany()
            .HasForeignKey(item => item.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Organization).WithMany()
            .HasForeignKey(item => item.OrganizationId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.ModeratorUserId, item.OrganizationId, item.Capability }).IsUnique();
        builder.HasIndex(item => new { item.OrganizationId, item.IsActive, item.ExpiresAt });
    }
}
