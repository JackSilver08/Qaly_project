using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.HasKey(vote => vote.Id);
        builder.Property(vote => vote.Id).HasDefaultValueSql("NEWID()");
        builder.Property(vote => vote.TargetType).HasMaxLength(20).IsRequired();
        builder.Property(vote => vote.Value).IsRequired();
        builder.Property(vote => vote.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(vote => vote.User)
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(vote => new { vote.TargetType, vote.TargetId, vote.UserId }).IsUnique();
        builder.HasIndex(vote => new { vote.TargetType, vote.TargetId });
    }
}
