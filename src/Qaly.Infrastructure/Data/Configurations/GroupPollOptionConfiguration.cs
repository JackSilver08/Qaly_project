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
        builder.Property(option => option.Text).HasMaxLength(300).IsRequired();
        builder.Property(option => option.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(option => option.GroupPoll)
            .WithMany(poll => poll.Options)
            .HasForeignKey(option => option.GroupPollId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(option => new { option.GroupPollId, option.SortOrder });
    }
}
