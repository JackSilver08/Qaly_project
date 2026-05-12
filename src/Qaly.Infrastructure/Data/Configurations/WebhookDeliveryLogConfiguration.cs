using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Qaly.Domain.Entities;

namespace Qaly.Infrastructure.Data.Configurations;

public class WebhookDeliveryLogConfiguration : IEntityTypeConfiguration<WebhookDeliveryLog>
{
    public void Configure(EntityTypeBuilder<WebhookDeliveryLog> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasDefaultValueSql("NEWID()");

        builder.Property(w => w.EventType).IsRequired().HasMaxLength(100);
        builder.Property(w => w.RequestPayload).HasColumnType("nvarchar(max)");
        builder.Property(w => w.ResponseBody).HasColumnType("nvarchar(max)");

        builder.HasOne(w => w.Webhook)
            .WithMany()
            .HasForeignKey(w => w.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => w.WebhookId);
        builder.HasIndex(w => w.CreatedAt); // For auto cleanup
    }
}
