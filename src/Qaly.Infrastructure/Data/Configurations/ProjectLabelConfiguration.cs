using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class ProjectLabelConfiguration : IEntityTypeConfiguration<ProjectLabel>
{
    public void Configure(EntityTypeBuilder<ProjectLabel> builder)
    {
        builder.HasKey(label => label.Id);
        builder.Property(label => label.Id).HasDefaultValueSql("NEWID()");
        builder.Property(label => label.Name).HasMaxLength(80).IsRequired();
        builder.Property(label => label.Color).HasMaxLength(20).IsRequired().HasDefaultValue("#64748B");
        builder.Property(label => label.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasOne(label => label.Project)
            .WithMany(project => project.Labels)
            .HasForeignKey(label => label.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(label => new { label.ProjectId, label.Name }).IsUnique();
    }
}
