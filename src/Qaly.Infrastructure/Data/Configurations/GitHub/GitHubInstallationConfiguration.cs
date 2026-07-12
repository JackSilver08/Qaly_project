using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities.GitHub;

namespace Qaly.Infrastructure.Data.Configurations.GitHub;

public class GitHubInstallationConfiguration : IEntityTypeConfiguration<GitHubInstallation>
{
    public void Configure(EntityTypeBuilder<GitHubInstallation> builder)
    {
        builder.ToTable("GitHubInstallations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("NEWID()");

        builder.Property(x => x.AccountLogin).HasMaxLength(255).IsRequired();
        builder.Property(x => x.AccountType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired().HasDefaultValue("Active");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");

        builder.HasIndex(x => x.InstallationId).IsUnique();
        builder.HasIndex(x => x.OrganizationId);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
