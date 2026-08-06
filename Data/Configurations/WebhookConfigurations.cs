using KorridorX.Models.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.HasIndex(x => x.ProviderCode);
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderEventId }).IsUnique();
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => x.ProcessingStatus);
        builder.HasIndex(x => x.ReceivedAt);

        builder.Property(x => x.ProviderEventId).HasMaxLength(150);
        builder.Property(x => x.EventType).HasMaxLength(150);
        builder.Property(x => x.SignatureHeader).HasMaxLength(1000);
        builder.Property(x => x.TimestampHeader).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
    }
}

public class WebhookProcessingAttemptConfiguration : IEntityTypeConfiguration<WebhookProcessingAttempt>
{
    public void Configure(EntityTypeBuilder<WebhookProcessingAttempt> builder)
    {
        builder.HasIndex(x => x.WebhookEventId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.StartedAt);

        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.HasOne(x => x.WebhookEvent)
            .WithMany(x => x.Attempts)
            .HasForeignKey(x => x.WebhookEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}