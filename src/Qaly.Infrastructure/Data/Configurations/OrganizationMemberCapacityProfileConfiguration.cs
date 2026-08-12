using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class OrganizationMemberCapacityProfileConfiguration : IEntityTypeConfiguration<OrganizationMemberCapacityProfile>
{
    public void Configure(EntityTypeBuilder<OrganizationMemberCapacityProfile> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.WeeklyCapacityHours).HasPrecision(6, 2);
        builder.Property(item => item.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne(item => item.Organization)
            .WithMany()
            .HasForeignKey(item => item.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(item => item.User)
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.OrganizationId, item.UserId }).IsUnique();
        builder.HasIndex(item => item.UserId);
    }
}
