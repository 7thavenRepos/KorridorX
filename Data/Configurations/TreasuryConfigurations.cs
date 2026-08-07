using KorridorX.Models.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KorridorX.Data.Configurations;

public sealed class ProviderWalletBalanceConfiguration : IEntityTypeConfiguration<ProviderWalletBalance>
{
    public void Configure(EntityTypeBuilder<ProviderWalletBalance> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.ProviderWalletId }).IsUnique();
        builder.HasIndex(x => new { x.ProviderCode, x.CurrencyCode });
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.ProviderWalletId).HasMaxLength(150);
        builder.Property(x => x.ProviderBusinessId).HasMaxLength(150);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.Balance).HasPrecision(18, 2);
    }
}

public sealed class LiquidityThresholdConfiguration : IEntityTypeConfiguration<LiquidityThreshold>
{
    public void Configure(EntityTypeBuilder<LiquidityThreshold> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.CurrencyCode, x.IsActive });
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.MinimumBalance).HasPrecision(18, 2);
        builder.Property(x => x.TargetBalance).HasPrecision(18, 2);
        builder.Property(x => x.MaximumBalance).HasPrecision(18, 2);
    }
}

public sealed class FxMarkupRuleConfiguration : IEntityTypeConfiguration<FxMarkupRule>
{
    public void Configure(EntityTypeBuilder<FxMarkupRule> builder)
    {
        builder.HasIndex(x => new { x.SourceCurrencyCode, x.DestinationCurrencyCode, x.IsActive });
        builder.Property(x => x.SourceCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.DestinationCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.MarkupPercentage).HasPrecision(9, 6);
        builder.Property(x => x.MinimumCustomerRate).HasPrecision(18, 8);
        builder.Property(x => x.MaximumCustomerRate).HasPrecision(18, 8);
    }
}

public sealed class SettlementBatchConfiguration : IEntityTypeConfiguration<SettlementBatch>
{
    public void Configure(EntityTypeBuilder<SettlementBatch> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.ProviderCode, x.CurrencyCode, x.WindowStart, x.WindowEnd });
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.GrossInflows).HasPrecision(18, 2);
        builder.Property(x => x.GrossOutflows).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedNetAmount).HasPrecision(18, 2);
        builder.Property(x => x.ActualNetAmount).HasPrecision(18, 2);
        builder.Property(x => x.VarianceAmount).HasPrecision(18, 2);
        builder.Property(x => x.ReconciliationNote).HasMaxLength(2000);
    }
}

public sealed class SettlementBatchItemConfiguration : IEntityTypeConfiguration<SettlementBatchItem>
{
    public void Configure(EntityTypeBuilder<SettlementBatchItem> builder)
    {
        builder.HasIndex(x => x.ProviderTransactionRowId).IsUnique();
        builder.HasIndex(x => x.SettlementBatchId);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.HasOne(x => x.SettlementBatch).WithMany(x => x.Items).HasForeignKey(x => x.SettlementBatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ProviderTransaction).WithMany().HasForeignKey(x => x.ProviderTransactionRowId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TreasuryRebalanceRequestConfiguration : IEntityTypeConfiguration<TreasuryRebalanceRequest>
{
    public void Configure(EntityTypeBuilder<TreasuryRebalanceRequest> builder)
    {
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.HasIndex(x => new { x.ProviderCode, x.Status, x.CreatedAt });
        builder.Property(x => x.Reference).HasMaxLength(100);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.FromCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ToCurrencyCode).HasMaxLength(10);
        builder.Property(x => x.RequestedAmount).HasPrecision(18, 2);
        builder.Property(x => x.FromAmount).HasPrecision(18, 2);
        builder.Property(x => x.FromAmountMinusFees).HasPrecision(18, 2);
        builder.Property(x => x.ToAmount).HasPrecision(18, 2);
        builder.Property(x => x.ExchangeRate).HasPrecision(18, 8);
        builder.Property(x => x.CustomExchangeRate).HasPrecision(18, 8);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.ProviderSwapId).HasMaxLength(150);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(150);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.FailureReason).HasMaxLength(2000);
        builder.HasOne(x => x.FromProviderWalletBalance).WithMany().HasForeignKey(x => x.FromProviderWalletBalanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ToProviderWalletBalance).WithMany().HasForeignKey(x => x.ToProviderWalletBalanceId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SettlementStatementImportConfiguration : IEntityTypeConfiguration<SettlementStatementImport>
{
    public void Configure(EntityTypeBuilder<SettlementStatementImport> builder)
    {
        builder.HasIndex(x => new { x.ProviderCode, x.FileHash }).IsUnique();
        builder.HasIndex(x => x.SettlementBatchId);
        builder.Property(x => x.ProviderCode).HasMaxLength(50);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.FileHash).HasMaxLength(64);
        builder.Property(x => x.GrossCredits).HasPrecision(18, 2);
        builder.Property(x => x.GrossDebits).HasPrecision(18, 2);
        builder.Property(x => x.NetAmount).HasPrecision(18, 2);
        builder.Property(x => x.VarianceAmount).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.HasOne(x => x.SettlementBatch).WithMany().HasForeignKey(x => x.SettlementBatchId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SettlementStatementItemConfiguration : IEntityTypeConfiguration<SettlementStatementItem>
{
    public void Configure(EntityTypeBuilder<SettlementStatementItem> builder)
    {
        builder.HasIndex(x => x.SettlementStatementImportId);
        builder.HasIndex(x => x.ProviderTransactionId);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(150);
        builder.Property(x => x.ProviderReference).HasMaxLength(150);
        builder.Property(x => x.TransactionType).HasMaxLength(100);
        builder.Property(x => x.CurrencyCode).HasMaxLength(10);
        builder.Property(x => x.ProviderStatus).HasMaxLength(100);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasOne(x => x.SettlementStatementImport).WithMany(x => x.Items).HasForeignKey(x => x.SettlementStatementImportId).OnDelete(DeleteBehavior.Cascade);
    }
}
