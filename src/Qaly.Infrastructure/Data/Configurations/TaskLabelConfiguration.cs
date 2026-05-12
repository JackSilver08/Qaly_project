using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class TaskLabelConfiguration : IEntityTypeConfiguration<TaskLabel>
{
    public void Configure(EntityTypeBuilder<TaskLabel> builder)
    {
        builder.HasKey(label => label.Id);
        builder.Property(label => label.Id).HasDefaultValueSql("NEWID()");
        builder.Property(label => label.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(label => label.TaskItem)
            .WithMany(task => task.Labels)
            .HasForeignKey(label => label.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(label => label.ProjectLabel)
            .WithMany(projectLabel => projectLabel.Tasks)
            .HasForeignKey(label => label.ProjectLabelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(label => new { label.TaskItemId, label.ProjectLabelId }).IsUnique();
    }
}
