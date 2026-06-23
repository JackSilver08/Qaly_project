using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class WorkGroupMemberConfiguration : IEntityTypeConfiguration<WorkGroupMember>
{
    public void Configure(EntityTypeBuilder<WorkGroupMember> builder)
    {
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Id).HasDefaultValueSql("NEWID()");
        builder.Property(member => member.Role).HasMaxLength(20).IsRequired();
        builder.Property(member => member.LastReadAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(member => member.IsMuted).HasDefaultValue(false);
        builder.Property(member => member.JoinedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(member => member.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(member => member.WorkGroup)
            .WithMany(group => group.Members)
            .HasForeignKey(member => member.WorkGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
            .WithMany(user => user.WorkGroupMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(member => new { member.WorkGroupId, member.UserId }).IsUnique();
        builder.HasIndex(member => member.UserId);
        builder.HasIndex(member => new { member.WorkGroupId, member.Role });
    }
}
