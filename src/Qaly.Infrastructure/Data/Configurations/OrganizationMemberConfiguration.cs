using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).HasDefaultValueSql("NEWID()");
        builder.Property(member => member.Role).HasMaxLength(20).IsRequired();
        builder.Property(member => member.JoinedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(member => member.Organization)
            .WithMany(organization => organization.Members)
            .HasForeignKey(member => member.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
            .WithMany(user => user.OrganizationMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(member => new { member.OrganizationId, member.UserId }).IsUnique();
        builder.HasIndex(member => member.UserId);
    }
}
