using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
    public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
    {
        builder.HasKey(request => request.Id);
        builder.Property(request => request.IdempotencyKey).HasMaxLength(160);
        builder.Property(request => request.RequestHash).HasMaxLength(64);
        builder.Property(request => request.PolicyVersion).HasMaxLength(80);
        builder.Property(request => request.ResultContentType).HasMaxLength(100);
        builder.Property(request => request.ResultFileName).HasMaxLength(200);
        builder.Property(request => request.LeaseOwner).HasMaxLength(200);
        builder.Property(request => request.LastErrorCode).HasMaxLength(100);
        builder.Property(request => request.AvailableAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
        builder.Property(request => request.MaxAttempts).HasDefaultValue(5);
        builder.Property(request => request.RowVersion).IsRowVersion();

        builder.HasIndex(request => new { request.AvailableAt, request.LeaseExpiresAt });
        builder.HasIndex(request => new { request.TenantId, request.SubjectUserId, request.RequestedAt });
        builder.HasIndex(request => new
            {
                request.TenantId,
                request.RequesterUserId,
                request.IdempotencyKey
            })
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .IsUnique();
    }
}
