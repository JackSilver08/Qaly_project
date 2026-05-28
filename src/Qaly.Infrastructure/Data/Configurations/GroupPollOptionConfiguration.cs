using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupPollOptionConfiguration : IEntityTypeConfiguration<GroupPollOption>
{
    public void Configure(EntityTypeBuilder<GroupPollOption> builder)
    {
        builder.HasKey(option => option.Id);
        builder.Property(option => option.Id).HasDefaultValueSql("NEWID()");
        builder.Property(option => option.Content).HasMaxLength(300).IsRequired();
        builder.Property(option => option.SortOrder).IsRequired();
        builder.Property(option => option.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(option => option.Poll)
            .WithMany(poll => poll.Options)
            .HasForeignKey(option => option.PollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(option => option.PollId);
        builder.HasIndex(option => new { option.PollId, option.SortOrder });
    }
}
