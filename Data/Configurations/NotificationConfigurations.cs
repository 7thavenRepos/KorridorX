using KorridorX.Models.Identity;
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
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.HasIndex(x => x.LockId);
        builder.HasIndex(x => x.DeadLetteredAt);
        builder.HasIndex(x => x.SentAt);
        builder.HasIndex(x => x.ReadAt);
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });

        builder.Property(x => x.Channel).HasMaxLength(50);
        builder.Property(x => x.Recipient).HasMaxLength(255);
        builder.Property(x => x.Subject).HasMaxLength(255);
        builder.Property(x => x.Status).HasMaxLength(50).IsConcurrencyToken();
        builder.Property(x => x.AttemptCount).HasDefaultValue(0);
        builder.Property(x => x.MaxAttempts).HasDefaultValue(5);
        builder.Property(x => x.ProviderMessageId).HasMaxLength(255);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100);
        builder.Property(x => x.RelatedEntityId).HasMaxLength(100);
    }
}

public class MobilePushDeviceConfiguration
    : IEntityTypeConfiguration<MobilePushDevice>
{
    public void Configure(
        EntityTypeBuilder<MobilePushDevice> builder)
    {
        builder.HasIndex(
            x => x.PushTokenHash)
            .IsUnique();

        builder.HasIndex(
            x => new
            {
                x.UserId,
                x.IsActive
            });

        builder.HasIndex(
            x => new
            {
                x.UserId,
                x.Platform,
                x.DeviceFingerprint
            });

        builder.Property(
            x => x.Platform)
            .HasMaxLength(20);

        builder.Property(
            x => x.PushToken)
            .HasMaxLength(4096);

        builder.Property(
            x => x.PushTokenHash)
            .HasMaxLength(64);

        builder.Property(
            x => x.DeviceFingerprint)
            .HasMaxLength(200);

        builder.Property(
            x => x.DeviceName)
            .HasMaxLength(200);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(
                x => x.UserId)
            .OnDelete(
                DeleteBehavior.Cascade);
    }
}
