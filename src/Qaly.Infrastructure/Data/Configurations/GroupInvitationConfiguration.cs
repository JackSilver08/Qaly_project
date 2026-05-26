using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupInvitationConfiguration : IEntityTypeConfiguration<GroupInvitation>
{
    public void Configure(EntityTypeBuilder<GroupInvitation> builder)
    {
        builder.HasKey(invitation => invitation.Id);
        builder.Property(invitation => invitation.Id).HasDefaultValueSql("NEWID()");
        builder.Property(invitation => invitation.Email).HasMaxLength(256).IsRequired();
        builder.Property(invitation => invitation.Token).HasMaxLength(128).IsRequired();
        builder.Property(invitation => invitation.Status).HasMaxLength(20).IsRequired();
        builder.Property(invitation => invitation.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(invitation => invitation.WorkGroup)
            .WithMany(group => group.Invitations)
            .HasForeignKey(invitation => invitation.WorkGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invitation => invitation.InvitedUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(invitation => invitation.Token).IsUnique();
        builder.HasIndex(invitation => new { invitation.WorkGroupId, invitation.Email, invitation.Status });
    }
}
