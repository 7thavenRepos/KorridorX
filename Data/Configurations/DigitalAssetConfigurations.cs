using KorridorX.Models.DigitalAssets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class DigitalAssetDepositAddressConfiguration : IEntityTypeConfiguration<DigitalAssetDepositAddress>
{
    public void Configure(EntityTypeBuilder<DigitalAssetDepositAddress> builder)
    {
        // Some networks/providers reuse one deposit address and distinguish
        // customers by destination tag / memo. PostgreSQL also treats NULL values
        // as distinct in a normal unique index, so use filtered indexes for both cases.
        builder.HasIndex(x => new
        {
            x.ProviderCode,
            x.AssetNetworkId,
            x.Address,
            x.DestinationTag
        })
            .IsUnique()
            .HasFilter("\"DestinationTag\" IS NOT NULL");

        builder.HasIndex(x => new
        {
            x.ProviderCode,
            x.AssetNetworkId,
            x.Address
        })
            .IsUnique()
            .HasFilter("\"DestinationTag\" IS NULL");

        builder.HasIndex(x => new { x.BusinessProfileId, x.BusinessCustomerId });
        builder.HasIndex(x => x.FinancialAccountId);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderAddressId).HasMaxLength(150);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.DestinationTag).HasMaxLength(150);
        builder.HasOne(x => x.FinancialAccount).WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssetNetwork).WithMany().HasForeignKey(x => x.AssetNetworkId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DigitalAssetWithdrawalDestinationConfiguration : IEntityTypeConfiguration<DigitalAssetWithdrawalDestination>
{
    public void Configure(EntityTypeBuilder<DigitalAssetWithdrawalDestination> builder)
    {
        // Enforce destination uniqueness correctly for both tagged and untagged
        // addresses. A single nullable unique index is insufficient on PostgreSQL
        // because multiple NULL values are allowed.
        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.BusinessCustomerId,
            x.AssetNetworkId,
            x.Address,
            x.DestinationTag
        })
            .IsUnique()
            .HasFilter("\"DestinationTag\" IS NOT NULL");

        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.BusinessCustomerId,
            x.AssetNetworkId,
            x.Address
        })
            .IsUnique()
            .HasFilter("\"DestinationTag\" IS NULL");

        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.DestinationTag).HasMaxLength(150);
        builder.Property(x => x.Label).HasMaxLength(150);
        builder.HasOne(x => x.AssetNetwork).WithMany().HasForeignKey(x => x.AssetNetworkId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DigitalAssetNetworkTransactionConfiguration : IEntityTypeConfiguration<DigitalAssetNetworkTransaction>
{
    public void Configure(EntityTypeBuilder<DigitalAssetNetworkTransaction> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderTransactionId }).IsUnique();
        builder.HasIndex(x => x.TransactionHash);
        builder.HasIndex(x => x.CollectionId);
        builder.HasIndex(x => x.PayoutId);
        builder.HasIndex(x => new { x.Status, x.Direction });
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.TransactionHash).HasMaxLength(300);
        builder.Property(x => x.FromAddress).HasMaxLength(300);
        builder.Property(x => x.ToAddress).HasMaxLength(300);
        builder.Property(x => x.DestinationTag).HasMaxLength(150);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.NetworkFee).HasPrecision(36, 18);
        builder.HasOne(x => x.AssetNetwork).WithMany().HasForeignKey(x => x.AssetNetworkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Collection).WithMany().HasForeignKey(x => x.CollectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LedgerTransaction).WithMany().HasForeignKey(x => x.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DigitalAssetWithdrawalConfiguration : IEntityTypeConfiguration<DigitalAssetWithdrawal>
{
    public void Configure(EntityTypeBuilder<DigitalAssetWithdrawal> builder)
    {
        builder.HasIndex(x => new { x.BusinessProfileId, x.BusinessCustomerId, x.CreatedAt });
        builder.HasIndex(x => x.PayoutId).IsUnique();
        builder.HasIndex(x => x.ReservationId).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.NetworkFee).HasPrecision(36, 18);
        builder.Property(x => x.TotalDebitAmount).HasPrecision(36, 18);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.HasOne(x => x.FinancialAccount).WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssetNetwork).WithMany().HasForeignKey(x => x.AssetNetworkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Destination).WithMany().HasForeignKey(x => x.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
    }
}
