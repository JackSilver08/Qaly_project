using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class TaskDependencyConfiguration : IEntityTypeConfiguration<TaskDependency>
{
    public void Configure(EntityTypeBuilder<TaskDependency> builder)
    {
        builder.HasKey(td => td.Id);
        builder.Property(td => td.Id).HasDefaultValueSql("NEWID()");

        builder.Property(td => td.DependencyType).HasMaxLength(20).IsRequired().HasDefaultValue("FinishToStart");

        builder.HasOne(td => td.Predecessor)
            .WithMany(t => t.SuccessorDependencies)
            .HasForeignKey(td => td.PredecessorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(td => td.Successor)
            .WithMany(t => t.PredecessorDependencies)
            .HasForeignKey(td => td.SuccessorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent self-dependency and duplicate dependencies
        builder.HasIndex(td => new { td.PredecessorId, td.SuccessorId }).IsUnique();
    }
}
