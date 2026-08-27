using KorridorX.Models.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.TransactionPinHash).HasMaxLength(1000);

        builder.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.LoginHistories)
            .WithOne(x => x.User)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsRevoked, x.ExpiresAt });

        builder.Property(x => x.Token).HasMaxLength(500);
        builder.Property(x => x.CreatedByIp).HasMaxLength(100);
        builder.Property(x => x.RevokedByIp).HasMaxLength(100);
        builder.Property(x => x.DeviceFingerprint).HasMaxLength(250);
        builder.Property(x => x.DeviceName).HasMaxLength(250);
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.Property(x => x.ReplacedByToken).HasMaxLength(128);
        builder.Property(x => x.RevokedReason).HasMaxLength(500);
        builder.Property(x => x.IsRevoked).IsConcurrencyToken();
    }
}

public class LoginHistoryConfiguration : IEntityTypeConfiguration<LoginHistory>
{
    public void Configure(EntityTypeBuilder<LoginHistory> builder)
    {
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.UserId, x.WasSuccessful, x.OccurredAt });
        builder.HasIndex(x => new { x.UserId, x.DeviceFingerprint, x.OccurredAt });

        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(1000);
        builder.Property(x => x.DeviceFingerprint).HasMaxLength(250);
        builder.Property(x => x.DeviceName).HasMaxLength(250);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.CountryName).HasMaxLength(100);
        builder.Property(x => x.Region).HasMaxLength(100);
        builder.Property(x => x.City).HasMaxLength(100);
        builder.Property(x => x.FailureReason).HasMaxLength(500);
    }
}
