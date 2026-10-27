using KorridorX.Models.AdminFunding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class AdminWalletFundingRequestConfiguration : IEntityTypeConfiguration<AdminWalletFundingRequest>
{
    public void Configure(EntityTypeBuilder<AdminWalletFundingRequest> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.RequestedByUserId, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.RequestedAtUtc });
        builder.HasIndex(x => x.SourceHouseAccountId);
        builder.HasIndex(x => x.DestinationWalletId);
        builder.HasIndex(x => x.LedgerTransactionId).IsUnique();
        builder.Property(x => x.Reference).HasMaxLength(60);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(160);
        builder.Property(x => x.RequestHash).HasMaxLength(64);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.Status).IsConcurrencyToken();
        // Deliberate scalar IDs: historical funding records survive account lifecycle changes.
    }
}
