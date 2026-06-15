using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class WorkGroupConfiguration : IEntityTypeConfiguration<WorkGroup>
{
    public void Configure(EntityTypeBuilder<WorkGroup> builder)
    {
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).HasDefaultValueSql("NEWID()");
        builder.Property(group => group.Name).HasMaxLength(160).IsRequired();
        builder.Property(group => group.AvatarUrl).HasMaxLength(1000);
        builder.Property(group => group.Color).HasMaxLength(20);
        builder.Property(group => group.BackgroundTheme).HasMaxLength(40);
        builder.Property(group => group.BackgroundImageUrl).HasMaxLength(1000);
        builder.Property(group => group.Status).HasMaxLength(20).IsRequired();
        builder.Property(group => group.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(group => group.Owner)
            .WithMany(user => user.OwnedWorkGroups)
            .HasForeignKey(group => group.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(group => group.Organization)
            .WithMany(organization => organization.WorkGroups)
            .HasForeignKey(group => group.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(group => group.OwnerId);
        builder.HasIndex(group => group.OrganizationId);
        builder.HasIndex(group => group.Status);
    }
}
