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
        builder.Property(invitation => invitation.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(invitation => invitation.ExpiredAt).IsRequired();
        builder.Property(invitation => invitation.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(invitation => invitation.Group)
            .WithMany(group => group.Invitations)
            .HasForeignKey(invitation => invitation.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(invitation => invitation.Token).IsUnique();
        builder.HasIndex(invitation => invitation.GroupId);
    }
}
