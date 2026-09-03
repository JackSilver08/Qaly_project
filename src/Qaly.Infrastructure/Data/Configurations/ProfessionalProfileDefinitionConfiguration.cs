using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class ProfessionalProfileDefinitionConfiguration : IEntityTypeConfiguration<ProfessionalProfileDefinition>
{
    public void Configure(EntityTypeBuilder<ProfessionalProfileDefinition> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ProfessionalProfileDefinitions_KeyName",
            "LEN([Key]) >= 2 AND LEN([Name]) >= 2 AND LEN([Category]) >= 2"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasDefaultValueSql("NEWID()");
        builder.Property(item => item.Key).HasMaxLength(80).IsRequired();
        builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(600);
        builder.Property(item => item.Category).HasMaxLength(80).IsRequired();
        builder.Property(item => item.IsSystemSeed).HasDefaultValue(false);
        builder.Property(item => item.IsActive).HasDefaultValue(true);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne(item => item.Organization)
            .WithMany(item => item.ProfessionalProfiles)
            .HasForeignKey(item => item.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.OrganizationId, item.Key }).IsUnique();
        builder.HasIndex(item => new { item.OrganizationId, item.IsActive, item.Category });
    }
}
