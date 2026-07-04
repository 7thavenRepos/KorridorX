using KorridorX.Models.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
    public void Configure(EntityTypeBuilder<NotificationMessage> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Channel);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.SentAt);
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });

        builder.Property(x => x.Channel).HasMaxLength(50);
        builder.Property(x => x.Recipient).HasMaxLength(255);
        builder.Property(x => x.Subject).HasMaxLength(255);
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(100);
    }
}