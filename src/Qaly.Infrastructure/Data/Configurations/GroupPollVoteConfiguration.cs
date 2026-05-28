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

        builder.HasOne(vote => vote.Poll)
            .WithMany(poll => poll.Votes)
            .HasForeignKey(vote => vote.PollId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(vote => vote.Option)
            .WithMany(option => option.Votes)
            .HasForeignKey(vote => vote.OptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(vote => vote.User)
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(vote => vote.PollId);
        builder.HasIndex(vote => vote.UserId);
        builder.HasIndex(vote => new { vote.PollId, vote.UserId });
        builder.HasIndex(vote => new { vote.PollId, vote.OptionId, vote.UserId }).IsUnique();
    }
}
