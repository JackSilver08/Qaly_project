using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class TaskAcceptanceChecklistItemConfiguration : IEntityTypeConfiguration<TaskAcceptanceChecklistItem>
{
    public void Configure(EntityTypeBuilder<TaskAcceptanceChecklistItem> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Text).HasMaxLength(1000).IsRequired();
        builder.Property(item => item.Kind).HasMaxLength(40).IsRequired().HasDefaultValue(TaskAcceptanceChecklistItem.Acceptance);
        builder.Property(item => item.RowVersion).IsRowVersion();
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.HasOne(item => item.Task).WithMany(task => task.AcceptanceChecklist)
            .HasForeignKey(item => item.TaskId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.CreatedByUser).WithMany()
            .HasForeignKey(item => item.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(item => new { item.TaskId, item.SortOrder }).IsUnique();
        builder.HasIndex(item => new { item.TaskId, item.Kind });
    }
}
