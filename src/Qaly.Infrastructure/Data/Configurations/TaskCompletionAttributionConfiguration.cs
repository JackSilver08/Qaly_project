using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class TaskCompletionAttributionConfiguration : IEntityTypeConfiguration<TaskCompletionAttribution>
{
    public void Configure(EntityTypeBuilder<TaskCompletionAttribution> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Status).HasMaxLength(32).IsRequired();
        builder.Property(item => item.AttributionPolicyVersion).HasMaxLength(80).IsRequired();
        builder.Property(item => item.CorrectionReason).HasMaxLength(500);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(item => item.TaskItem)
            .WithMany(task => task.CompletionAttributions)
            .HasForeignKey(item => item.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ContributorUser)
            .WithMany()
            .HasForeignKey(item => item.ContributorUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ConfirmedByUser)
            .WithMany()
            .HasForeignKey(item => item.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TaskItemId, item.ContributorUserId }).IsUnique();
        builder.HasIndex(item => new { item.ContributorUserId, item.Status });
        builder.HasIndex(item => new { item.TaskItemId, item.Status });
    }
}
