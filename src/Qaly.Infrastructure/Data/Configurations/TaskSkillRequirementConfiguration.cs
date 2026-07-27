using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class TaskSkillRequirementConfiguration : IEntityTypeConfiguration<TaskSkillRequirement>
{
    public void Configure(EntityTypeBuilder<TaskSkillRequirement> builder)
    {
        builder.HasKey(requirement => requirement.Id);
        builder.Property(requirement => requirement.Id).HasDefaultValueSql("NEWID()");
        builder.Property(requirement => requirement.RequiredLevel).HasMaxLength(20).IsRequired();
        builder.Property(requirement => requirement.Provenance).HasMaxLength(20).IsRequired();
        builder.Property(requirement => requirement.RowVersion).IsRowVersion();
        builder.Property(requirement => requirement.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(requirement => requirement.TaskItem)
            .WithMany(task => task.SkillRequirements)
            .HasForeignKey(requirement => requirement.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(requirement => requirement.OrganizationSkill)
            .WithMany(skill => skill.TaskRequirements)
            .HasForeignKey(requirement => requirement.OrganizationSkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(requirement => requirement.ConfirmedByUser)
            .WithMany()
            .HasForeignKey(requirement => requirement.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(requirement => new { requirement.TaskItemId, requirement.OrganizationSkillId }).IsUnique();
        builder.HasIndex(requirement => requirement.OrganizationSkillId);
        builder.HasIndex(requirement => requirement.ConfirmedByUserId);
    }
}
