using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).HasDefaultValueSql("NEWID()");
        builder.Property(assignment => assignment.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(assignment => assignment.TaskItem)
            .WithMany(task => task.Assignees)
            .HasForeignKey(assignment => assignment.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(assignment => assignment.User)
            .WithMany()
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(assignment => new { assignment.TaskItemId, assignment.UserId }).IsUnique();
        builder.HasIndex(assignment => assignment.UserId);
    }
}
