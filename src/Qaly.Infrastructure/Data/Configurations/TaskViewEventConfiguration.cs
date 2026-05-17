using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class TaskViewEventConfiguration : IEntityTypeConfiguration<TaskViewEvent>
{
    public void Configure(EntityTypeBuilder<TaskViewEvent> builder)
    {
        builder.HasKey(view => view.Id);
        builder.Property(view => view.Id).HasDefaultValueSql("NEWID()");
        builder.Property(view => view.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(view => view.ViewedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(view => view.ViewCount).HasDefaultValue(1);

        builder.HasOne(view => view.TaskItem)
            .WithMany(task => task.ViewEvents)
            .HasForeignKey(view => view.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(view => view.User)
            .WithMany()
            .HasForeignKey(view => view.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(view => new { view.TaskItemId, view.UserId }).IsUnique();
        builder.HasIndex(view => view.ViewedAt);
    }
}
