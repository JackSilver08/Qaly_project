using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupMessageUserStateConfiguration : IEntityTypeConfiguration<GroupMessageUserState>
{
    public void Configure(EntityTypeBuilder<GroupMessageUserState> builder)
    {
        builder.HasKey(state => state.Id);
        builder.Property(state => state.Id).HasDefaultValueSql("NEWID()");
        builder.Property(state => state.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(state => state.GroupMessage)
            .WithMany(message => message.UserStates)
            .HasForeignKey(state => state.GroupMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(state => state.User)
            .WithMany()
            .HasForeignKey(state => state.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(state => new { state.GroupMessageId, state.UserId })
            .IsUnique();
        builder.HasIndex(state => new { state.UserId, state.HiddenAt });
    }
}
