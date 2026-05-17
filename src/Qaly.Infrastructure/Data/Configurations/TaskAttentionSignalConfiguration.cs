using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class TaskAttentionSignalConfiguration : IEntityTypeConfiguration<TaskAttentionSignal>
{
    public void Configure(EntityTypeBuilder<TaskAttentionSignal> builder)
    {
        builder.HasKey(signal => signal.Id);
        builder.Property(signal => signal.Id).HasDefaultValueSql("NEWID()");
        builder.Property(signal => signal.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(signal => signal.SignalType).HasMaxLength(50).IsRequired();
        builder.Property(signal => signal.FirstDetectedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(signal => signal.CooldownHours).HasDefaultValue(24);

        builder.HasOne(signal => signal.TaskItem)
            .WithMany()
            .HasForeignKey(signal => signal.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(signal => signal.User)
            .WithMany()
            .HasForeignKey(signal => signal.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(signal => new { signal.TaskItemId, signal.UserId, signal.SignalType }).IsUnique();
        builder.HasIndex(signal => new { signal.UserId, signal.ResolvedAt, signal.LastSentAt });
    }
}
