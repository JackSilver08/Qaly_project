using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupMessageConfiguration : IEntityTypeConfiguration<GroupMessage>
{
    public void Configure(EntityTypeBuilder<GroupMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).HasDefaultValueSql("NEWID()");
        builder.Property(message => message.Content).HasMaxLength(4000).IsRequired();
        builder.Property(message => message.MessageType).HasMaxLength(30).IsRequired();
        builder.Property(message => message.IsDeleted).HasDefaultValue(false);
        builder.Property(message => message.IsPinned).HasDefaultValue(false);
        builder.Property(message => message.ReactionSummaryJson).HasMaxLength(4000).HasDefaultValue("[]");
        builder.Property(message => message.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(message => message.WorkGroup)
            .WithMany(group => group.Messages)
            .HasForeignKey(message => message.WorkGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(message => message.User)
            .WithMany(user => user.GroupMessages)
            .HasForeignKey(message => message.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(message => new { message.WorkGroupId, message.CreatedAt });
        builder.HasIndex(message => new { message.WorkGroupId, message.IsPinned });
        builder.HasIndex(message => message.UserId);
    }
}
