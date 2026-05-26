using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupPollVoteConfiguration : IEntityTypeConfiguration<GroupPollVote>
{
    public void Configure(EntityTypeBuilder<GroupPollVote> builder)
    {
        builder.HasKey(vote => vote.Id);
        builder.Property(vote => vote.Id).HasDefaultValueSql("NEWID()");
        builder.Property(vote => vote.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(vote => vote.GroupPoll)
            .WithMany(poll => poll.Votes)
            .HasForeignKey(vote => vote.GroupPollId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(vote => vote.GroupPollOption)
            .WithMany(option => option.Votes)
            .HasForeignKey(vote => vote.GroupPollOptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(vote => vote.User)
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(vote => new { vote.GroupPollId, vote.GroupPollOptionId, vote.UserId }).IsUnique();
        builder.HasIndex(vote => new { vote.GroupPollId, vote.UserId });
    }
}
