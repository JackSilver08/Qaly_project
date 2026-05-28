using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupPollConfiguration : IEntityTypeConfiguration<GroupPoll>
{
    public void Configure(EntityTypeBuilder<GroupPoll> builder)
    {
        builder.HasKey(poll => poll.Id);
        builder.Property(poll => poll.Id).HasDefaultValueSql("NEWID()");
        builder.Property(poll => poll.Question).HasMaxLength(500).IsRequired();
        builder.Property(poll => poll.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(poll => poll.AllowMultiple).HasDefaultValue(false);
        builder.Property(poll => poll.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(poll => poll.Group)
            .WithMany(group => group.Polls)
            .HasForeignKey(poll => poll.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(poll => poll.CreatedByUser)
            .WithMany()
            .HasForeignKey(poll => poll.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(poll => poll.GroupId);
        builder.HasIndex(poll => poll.CreatedByUserId);
    }
}
