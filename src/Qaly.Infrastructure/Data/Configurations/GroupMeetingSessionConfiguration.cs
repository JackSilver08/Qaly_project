using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class GroupMeetingSessionConfiguration : IEntityTypeConfiguration<GroupMeetingSession>
{
    public void Configure(EntityTypeBuilder<GroupMeetingSession> builder)
    {
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Id).HasDefaultValueSql("NEWID()");
        builder.Property(session => session.Provider).HasMaxLength(50).IsRequired();
        builder.Property(session => session.RoomId).HasMaxLength(200).IsRequired();
        builder.Property(session => session.JoinUrl).HasMaxLength(1000);
        builder.Property(session => session.Status).HasMaxLength(20).IsRequired();
        builder.Property(session => session.TranscriptSourceId).HasMaxLength(200);
        builder.Property(session => session.Summary).HasMaxLength(4000);
        builder.Property(session => session.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(session => session.StartedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(session => session.WorkGroup)
            .WithMany(group => group.MeetingSessions)
            .HasForeignKey(session => session.WorkGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(session => session.StartedByUser)
            .WithMany()
            .HasForeignKey(session => session.StartedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(session => new { session.WorkGroupId, session.Status, session.StartedAt });
        builder.HasIndex(session => session.RoomId);
    }
}
