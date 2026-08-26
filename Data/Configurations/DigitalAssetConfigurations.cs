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
        // Embedded Finance customer destinations remain unique within
        // (business profile + business customer + network + address/tag).
        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.BusinessCustomerId,
            x.AssetNetworkId,
            x.Address,
            x.DestinationTag
        })
            .IsUnique()
            .HasFilter("\"BusinessCustomerId\" IS NOT NULL AND \"DestinationTag\" IS NOT NULL");

        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.BusinessCustomerId,
            x.AssetNetworkId,
            x.Address
        })
            .IsUnique()
            .HasFilter("\"BusinessCustomerId\" IS NOT NULL AND \"DestinationTag\" IS NULL");

        // Direct BusinessProfile destinations have BusinessCustomerId = NULL.
        // PostgreSQL treats NULLs as distinct in normal unique indexes, so use
        // separate filtered indexes that omit BusinessCustomerId from the key.
        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.AssetNetworkId,
            x.Address,
            x.DestinationTag
        })
            .IsUnique()
            .HasFilter("\"BusinessCustomerId\" IS NULL AND \"DestinationTag\" IS NOT NULL");

        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.AssetNetworkId,
            x.Address
        })
            .IsUnique()
            .HasFilter("\"BusinessCustomerId\" IS NULL AND \"DestinationTag\" IS NULL");

        builder.HasIndex(x => new { x.BusinessProfileId, x.BusinessCustomerId });
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
        builder.Property(x => x.ActualNetworkFee).HasPrecision(36, 18);
        builder.Property(x => x.NetworkFeeVariance).HasPrecision(36, 18);
        builder.Property(x => x.TotalDebitAmount).HasPrecision(36, 18);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.HasOne(x => x.FinancialAccount).WithMany().HasForeignKey(x => x.FinancialAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssetNetwork).WithMany().HasForeignKey(x => x.AssetNetworkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Destination).WithMany().HasForeignKey(x => x.DestinationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Payout).WithMany().HasForeignKey(x => x.PayoutId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DigitalAssetProviderConfigurationConfiguration :
    IEntityTypeConfiguration<DigitalAssetProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<DigitalAssetProviderConfiguration> builder)
    {
        builder.HasIndex(x => x.ProviderCode).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.DisplayName).HasMaxLength(120);
        builder.Property(x => x.BaseUrl).HasMaxLength(1000);
        builder.Property(x => x.WebhookSecretProtected).HasColumnType("text");
        builder.Property(x => x.WebhookSecretLastFour).HasMaxLength(4);
        builder.Property(x => x.MetadataJson).HasColumnType("text");
        builder.Property(x => x.LastHealthCheckMessage).HasMaxLength(1000);
    }
}

public sealed class DigitalAssetWebhookReceiptConfiguration :
    IEntityTypeConfiguration<DigitalAssetWebhookReceipt>
{
    public void Configure(EntityTypeBuilder<DigitalAssetWebhookReceipt> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderEventId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.ReceivedAt });
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderEventId).HasMaxLength(200);
        builder.Property(x => x.PayloadHash).HasMaxLength(64);
        builder.Property(x => x.EventType).HasMaxLength(150);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
    }
}

public sealed class DigitalAssetAddressRiskAssessmentConfiguration :
    IEntityTypeConfiguration<DigitalAssetAddressRiskAssessment>
{
    public void Configure(EntityTypeBuilder<DigitalAssetAddressRiskAssessment> builder)
    {
        builder.HasIndex(x => new
        {
            x.BusinessProfileId,
            x.BusinessCustomerId,
            x.AssetNetworkId,
            x.Address,
            x.Direction,
            x.AssessedAt
        });
        builder.HasIndex(x => new { x.IsBlocking, x.RiskLevel, x.AssessedAt });
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.RiskScore).HasPrecision(9, 6);
        builder.Property(x => x.ReasonsJson).HasColumnType("text");
        builder.Property(x => x.RawResultJson).HasColumnType("text");
        builder.HasOne(x => x.AssetNetwork).WithMany()
            .HasForeignKey(x => x.AssetNetworkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DigitalAssetTravelRuleRecordConfiguration :
    IEntityTypeConfiguration<DigitalAssetTravelRuleRecord>
{
    public void Configure(EntityTypeBuilder<DigitalAssetTravelRuleRecord> builder)
    {
        builder.HasIndex(x => x.DigitalAssetWithdrawalId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.Property(x => x.ThresholdAmount).HasPrecision(36, 18);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.NetworkCode).HasMaxLength(50);
        builder.Property(x => x.OriginatorVasp).HasMaxLength(200);
        builder.Property(x => x.BeneficiaryVasp).HasMaxLength(200);
        builder.Property(x => x.BeneficiaryName).HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.PayloadJson).HasColumnType("text");
        builder.Property(x => x.FailureReason).HasMaxLength(2000);
        builder.HasOne(x => x.DigitalAssetWithdrawal).WithMany()
            .HasForeignKey(x => x.DigitalAssetWithdrawalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}


public sealed class DigitalAssetDepositIntentConfiguration :
    IEntityTypeConfiguration<DigitalAssetDepositIntent>
{
    public void Configure(EntityTypeBuilder<DigitalAssetDepositIntent> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderCollectionId })
            .IsUnique()
            .HasFilter("\"ProviderCollectionId\" IS NOT NULL");
        builder.HasIndex(x => new { x.BusinessProfileId, x.BusinessCustomerId, x.CreatedAt });
        builder.HasIndex(x => x.FinancialAccountId);
        builder.HasIndex(x => new { x.Status, x.ProviderExpiresAt });
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderWalletId).HasMaxLength(150);
        builder.Property(x => x.ProviderCollectionId).HasMaxLength(200);
        builder.Property(x => x.ProviderReference).HasMaxLength(200);
        builder.Property(x => x.AssetCode).HasMaxLength(20);
        builder.Property(x => x.NetworkCode).HasMaxLength(50);
        builder.Property(x => x.Amount).HasPrecision(36, 18);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.FailureReason).HasMaxLength(2000);
        builder.HasOne(x => x.FinancialAccount).WithMany()
            .HasForeignKey(x => x.FinancialAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssetNetwork).WithMany()
            .HasForeignKey(x => x.AssetNetworkId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Collection).WithMany()
            .HasForeignKey(x => x.CollectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}